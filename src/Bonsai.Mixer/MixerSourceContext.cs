using System;
using System.Reactive.Subjects;
using OpenCV.Net;
using PortAudioNet;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents a single audio source playing as one voice over a mixer context.
    /// </summary>
    /// <remarks>
    /// A source maintains an ordered queue of audio buffers played back through a single read
    /// cursor, scaled by a source gain and an independent per-channel gain, each of which can
    /// vary over time. The source is an opaque handle created by the mixer and controlled by
    /// downstream operators that append, clear or replace buffers, set the gain, or stop playback.
    /// </remarks>
    public unsafe class MixerSourceContext
    {
        readonly int channelCount;
        readonly double sampleRate;
        readonly bool looping;
        readonly bool removeOnComplete;
        readonly RingList<Mat> sourceBuffers = new();
        readonly WorkQueue<Action> commandQueue = new();
        readonly LinearRamp sourceGain;
        readonly LinearRamp transitionGain;
        readonly LinearRamp[] channelGains;
        readonly BehaviorSubject<MixerSourceState> playbackState;

        int bufferIndex;
        int sampleIndex;
        int clearCount;
        int gapSamples;
        int gapRemaining;
        bool clearing;
        bool stopping;
        bool playing;
        MixerSourceState currentState;
        MixerSourceState reportedState;

        internal MixerSourceContext(int channelCount, double sampleRate, Mat initialBuffer, bool looping, bool playing, float initialGain, bool removeOnComplete)
        {
            this.channelCount = channelCount;
            this.sampleRate = sampleRate;
            this.looping = looping;
            this.playing = playing;
            this.removeOnComplete = removeOnComplete;
            currentState = reportedState = playing ? MixerSourceState.Playing : MixerSourceState.Paused;
            playbackState = new BehaviorSubject<MixerSourceState>(currentState);
            sourceGain = new LinearRamp(sampleRate, initialGain);
            transitionGain = new LinearRamp(sampleRate, 1f);
            channelGains = new LinearRamp[channelCount];
            for (int i = 0; i < channelCount; i++)
                channelGains[i] = new LinearRamp(sampleRate, 1f);
            if (initialBuffer is not null)
            {
                Validate(initialBuffer);
                sourceBuffers.Add(initialBuffer);
            }
        }

        void Validate(Mat buffer)
        {
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));

            if (buffer.Rows != 1 && buffer.Rows != channelCount)
                throw new ArgumentException(
                    "The number of rows in the sample buffer must be one, for a mono source, or equal to the number of channels.",
                    nameof(buffer));

            if (buffer.Cols < 1)
                throw new ArgumentException(
                    "The sample buffer must contain at least one sample.",
                    nameof(buffer));

            if (buffer.Depth != Depth.F32)
                throw new ArgumentException(
                    $"Invalid sample depth '{buffer.Depth}'. All samples must be {Depth.F32}.",
                    nameof(buffer));
        }

        /// <summary>
        /// Appends an audio buffer to the end of the source playback queue.
        /// </summary>
        /// <param name="buffer">
        /// A multi-dimensional array containing the sample data, where each row holds the samples
        /// for one channel. A single-row buffer is played as a mono source and distributed across
        /// the output channels by the per-channel gain.
        /// </param>
        /// <exception cref="ArgumentNullException">The buffer is null.</exception>
        /// <exception cref="ArgumentException">
        /// The number of rows is neither one nor the number of channels in the stream, the buffer
        /// contains no samples, or the sample depth is not 32-bit floating point.
        /// </exception>
        public void Append(Mat buffer)
        {
            Validate(buffer);
            commandQueue.Add(() => sourceBuffers.Add(buffer));
        }

        /// <summary>
        /// Clears the source playback queue, fading the queued audio to silence over the
        /// specified duration before it is discarded.
        /// </summary>
        /// <remarks>
        /// Every buffer queued when the command is processed is discarded, including the one
        /// currently playing, while buffers appended afterwards are kept and start playing once
        /// the fade completes. Unlike <see cref="Stop"/>, the source stays on the mixer.
        /// </remarks>
        /// <param name="fadeDuration">
        /// The length of the fade-out, in seconds. A duration of zero discards the queue immediately.
        /// </param>
        public void Clear(double fadeDuration)
        {
            commandQueue.Add(() => BeginClear(fadeDuration));
        }

        /// <summary>
        /// Replaces the source playback queue with a single audio buffer, fading the queued audio
        /// to silence over the specified duration before the new buffer starts playing.
        /// </summary>
        /// <remarks>
        /// The queue is discarded as in <see cref="Clear"/> and the new buffer is queued in the
        /// same step. The new buffer starts from its first sample once the fade completes, or as
        /// soon as the queued audio runs out. It never overlaps the audio it replaces.
        /// </remarks>
        /// <param name="buffer">
        /// A multi-dimensional array containing the sample data, where each row holds the samples
        /// for one channel. A single-row buffer is played as a mono source and distributed across
        /// the output channels by the per-channel gain.
        /// </param>
        /// <param name="fadeDuration">
        /// The length of the fade-out, in seconds. A duration of zero discards the queue immediately.
        /// </param>
        /// <exception cref="ArgumentNullException">The buffer is null.</exception>
        /// <exception cref="ArgumentException">
        /// The number of rows is neither one nor the number of channels in the stream, the buffer
        /// contains no samples, or the sample depth is not 32-bit floating point.
        /// </exception>
        public void Replace(Mat buffer, double fadeDuration)
        {
            Replace(buffer, fadeDuration, 0);
        }

        /// <summary>
        /// Replaces the source playback queue with a single audio buffer, fading the queued audio
        /// to silence over the specified duration and holding a gap of silence before the new
        /// buffer starts playing.
        /// </summary>
        /// <remarks>
        /// The queue is discarded as in <see cref="Replace(Mat, double)"/>. The gap starts on the
        /// sample after the fade completes, or after the queued audio runs out if that happens
        /// first, and the new buffer starts from its first sample once the gap has elapsed. The gap
        /// is played only once, even on a looping source. If the source is paused during the gap,
        /// the countdown stops and the rest of the gap plays when playback resumes.
        /// A clear or replace arriving during the gap discards the waiting buffer immediately,
        /// since there is nothing audible to fade.
        /// </remarks>
        /// <param name="buffer">
        /// A multi-dimensional array containing the sample data, where each row holds the samples
        /// for one channel. A single-row buffer is played as a mono source and distributed across
        /// the output channels by the per-channel gain.
        /// </param>
        /// <param name="fadeDuration">
        /// The length of the fade-out, in seconds. A duration of zero discards the queue immediately.
        /// </param>
        /// <param name="gapDuration">
        /// The length of the silence between the end of the fade-out and the start of the new
        /// buffer, in seconds. A duration of zero starts the new buffer as soon as the fade ends.
        /// </param>
        /// <exception cref="ArgumentNullException">The buffer is null.</exception>
        /// <exception cref="ArgumentException">
        /// The number of rows is neither one nor the number of channels in the stream, the buffer
        /// contains no samples, or the sample depth is not 32-bit floating point.
        /// </exception>
        public void Replace(Mat buffer, double fadeDuration, double gapDuration)
        {
            Validate(buffer);
            var gapSamples = (int)Math.Max(0, gapDuration * sampleRate);
            commandQueue.Add(() =>
            {
                BeginClear(fadeDuration, gapSamples);
                sourceBuffers.Add(buffer);
            });
        }

        void BeginClear(double fadeDuration, int gapSamples = 0)
        {
            clearing = true;
            clearCount = sourceBuffers.Count;
            this.gapSamples = gapSamples;
            if (gapRemaining > 0)
            {
                // nothing is audible during a gap and the waiting buffer is dropped at once
                gapRemaining = 0;
                transitionGain.SetTarget(0f, 0);
            }
            else transitionGain.SetTarget(0f, fadeDuration);
        }

        void CompleteClear()
        {
            sourceBuffers.RemoveRange(clearCount);
            bufferIndex = 0;
            sampleIndex = 0;
            clearCount = 0;
            clearing = false;
            gapRemaining = gapSamples;
            gapSamples = 0;
            transitionGain.SetTarget(1f, 0);
        }

        int PlayableCount => clearing ? clearCount : sourceBuffers.Count;

        bool IsExhausted()
        {
            var playableCount = PlayableCount;
            return bufferIndex >= playableCount && !(looping && playableCount > 0);
        }

        /// <summary>
        /// Sets the source gain to a new level over the specified duration.
        /// </summary>
        /// <remarks>
        /// The source gain scales every output channel uniformly and is independent of the
        /// per-channel gain. The effective gain applied to a channel is the product of the source
        /// gain and the per-channel gain for that channel.
        /// </remarks>
        /// <param name="value">
        /// The gain level, where 1 represents the original signal amplitude.
        /// </param>
        /// <param name="duration">
        /// The length of the gain ramp, in seconds. A duration of zero changes the gain immediately.
        /// </param>
        public void SetGain(float value, double duration)
        {
            commandQueue.Add(() => sourceGain.SetTarget(value, duration));
        }

        /// <summary>
        /// Sets the per-channel gain of the source to a separate level for each output channel over
        /// the specified duration.
        /// </summary>
        /// <remarks>
        /// The per-channel gain is independent of the source gain. The effective gain applied to a
        /// channel is the product of the source gain and the per-channel gain for that channel.
        /// </remarks>
        /// <param name="values">
        /// The gain level for each output channel, where 1 represents the original signal
        /// amplitude. The number of values must be equal to the number of channels.
        /// </param>
        /// <param name="duration">
        /// The length of the gain ramp, in seconds. A duration of zero changes the gain immediately.
        /// </param>
        /// <exception cref="ArgumentNullException">The array of values is null.</exception>
        /// <exception cref="ArgumentException">
        /// The number of values is not equal to the number of channels.
        /// </exception>
        public void SetChannelGain(float[] values, double duration)
        {
            if (values is null)
                throw new ArgumentNullException(nameof(values));

            if (values.Length != channelCount)
                throw new ArgumentException(
                    "The number of gain values must be equal to the number of channels.",
                    nameof(values));

            commandQueue.Add(() =>
            {
                for (int i = 0; i < channelGains.Length; i++)
                    channelGains[i].SetTarget(values[i], duration);
            });
        }

        /// <summary>
        /// Resumes playback of the source from its current position.
        /// </summary>
        /// <remarks>
        /// Playback resumes from the buffer and sample where it was paused. Resuming a source
        /// that is already playing has no effect.
        /// </remarks>
        public void Play()
        {
            commandQueue.Add(() => playing = true);
        }

        /// <summary>
        /// Pauses playback of the source, holding its position until playback is resumed.
        /// </summary>
        /// <remarks>
        /// The playback cursor and queued buffers are retained while paused, and the source
        /// stops contributing to the output without being removed. Pausing a source that is
        /// already paused has no effect.
        /// </remarks>
        public void Pause()
        {
            commandQueue.Add(() => playing = false);
        }

        /// <summary>
        /// Stops playback of the source, fading the gain to silence over the specified duration
        /// before the source is removed from the mixer.
        /// </summary>
        /// <param name="fadeDuration">
        /// The length of the fade-out, in seconds. A duration of zero stops playback immediately.
        /// </param>
        public void Stop(double fadeDuration)
        {
            commandQueue.Add(() =>
            {
                stopping = true;
                sourceGain.SetTarget(0f, fadeDuration);
            });
        }

        /// <summary>
        /// Gets an observable sequence that reports the playback state of the source, starting with
        /// its current state and emitting each transition, then completing when the source is
        /// removed from the mixer.
        /// </summary>
        /// <remarks>
        /// Notifications are delivered off the audio thread. The sequence is backed by a
        /// <see cref="BehaviorSubject{T}"/>, so a subscriber that connects late receives the current
        /// state immediately.
        /// </remarks>
        internal IObservable<MixerSourceState> PlaybackState => playbackState;

        internal bool TryGetStateChange(out MixerSourceState newState)
        {
            if (currentState != reportedState)
            {
                newState = reportedState = currentState;
                return true;
            }

            newState = default;
            return false;
        }

        internal void NotifyState(MixerSourceState newState)
        {
            playbackState.OnNext(newState);
            if (newState == MixerSourceState.Stopped)
                playbackState.OnCompleted();
        }

        internal void DispatchCommands()
        {
            commandQueue.DispatchReady(command =>
            {
                command();
                return true;
            });
        }

        internal PaStreamCallbackResult StreamCallback(float** output, uint frameCount, in RenderContext render, in PaStreamCallbackTimeInfo timeInfo, PaStreamCallbackFlags statusFlags)
        {
            DispatchCommands();

            if (!playing)
            {
                if (stopping)
                    return PaStreamCallbackResult.Complete;

                if (clearing)
                    CompleteClear();
                currentState = MixerSourceState.Paused;
                return PaStreamCallbackResult.Continue;
            }

            int frame = 0;
            float* sourceGainBuffer = render.SourceGainBuffer;
            float* channelGainBuffer = render.ChannelGainBuffer;
            float* transitionGainBuffer = render.TransitionGainBuffer;
            while (frame < frameCount)
            {
                if (clearing && transitionGain.IsCompleted)
                    CompleteClear();

                if (gapRemaining > 0)
                {
                    // the gap plays as silence: nothing is written to the output while the gain
                    // ramps keep advancing and fades and stops still run to time
                    var silence = Math.Min(Math.Min(gapRemaining, (int)frameCount - frame), RenderContext.BlockSize);
                    sourceGain.Generate(new Span<float>(sourceGainBuffer, silence));
                    for (int channel = 0; channel < channelCount; channel++)
                        channelGains[channel].Generate(new Span<float>(channelGainBuffer + channel * RenderContext.BlockSize, silence));
                    gapRemaining -= silence;
                    frame += silence;
                    continue;
                }

                var playableCount = PlayableCount;
                if (bufferIndex >= playableCount)
                {
                    if (looping && playableCount > 0)
                    {
                        bufferIndex = 0;
                        sampleIndex = 0;
                    }
                    else if (clearing)
                    {
                        CompleteClear();
                        continue;
                    }
                    else break;
                }

                var currentBuffer = sourceBuffers[bufferIndex];
                currentBuffer.GetRawData(out IntPtr dataPtr, out int step);
                var samples = (float*)dataPtr.ToPointer();
                var stepSamples = step / sizeof(float);
                var bufferRows = currentBuffer.Rows;
                var bufferColumns = currentBuffer.Cols;

                var available = bufferColumns - sampleIndex;
                var count = Math.Min(Math.Min(available, (int)frameCount - frame), RenderContext.BlockSize);
                if (clearing)
                {
                    count = Math.Min(count, transitionGain.RemainingSamples);
                    sourceGain.Generate(new Span<float>(sourceGainBuffer, count));
                    transitionGain.Generate(new Span<float>(transitionGainBuffer, count));
                    for (int i = 0; i < count; i++)
                        sourceGainBuffer[i] *= transitionGainBuffer[i];
                }
                else sourceGain.Generate(new Span<float>(sourceGainBuffer, count));

                for (int channel = 0; channel < channelCount; channel++)
                {
                    var channelGain = channelGainBuffer + channel * RenderContext.BlockSize;
                    channelGains[channel].Generate(new Span<float>(channelGain, count));

                    var row = bufferRows == 1 ? 0 : channel;
                    float* source = samples + row * stepSamples + sampleIndex;
                    float* destination = output[channel] + frame;
                    for (int i = 0; i < count; i++)
                        destination[i] += sourceGainBuffer[i] * channelGain[i] * source[i];
                }

                sampleIndex += count;
                frame += count;
                if (sampleIndex >= bufferColumns)
                {
                    bufferIndex++;
                    sampleIndex = 0;
                }
            }

            if (!looping && !removeOnComplete && bufferIndex > 0)
            {
                sourceBuffers.RemoveRange(bufferIndex);
                if (clearing)
                    clearCount -= bufferIndex;
                bufferIndex = 0;
            }

            var exhausted = IsExhausted();
            if (clearing && exhausted)
            {
                CompleteClear();
                exhausted = IsExhausted();
            }

            if (stopping)
            {
                if (sourceGain.IsCompleted || exhausted)
                    return PaStreamCallbackResult.Complete;

                currentState = MixerSourceState.Playing;
                return PaStreamCallbackResult.Continue;
            }

            if (removeOnComplete && sourceBuffers.Count > 0 && exhausted)
                return PaStreamCallbackResult.Complete;

            currentState = exhausted ? MixerSourceState.Idle : MixerSourceState.Playing;
            return PaStreamCallbackResult.Continue;
        }
    }
}

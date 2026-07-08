using System;
using System.Reactive.Subjects;
using OpenCV.Net;
using PortAudioNet;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents a single audio source playing as one voice over a mixer stream context.
    /// </summary>
    /// <remarks>
    /// A source maintains an ordered queue of audio buffers played back through a single read
    /// cursor, scaled by a per-channel gain that can vary over time. The source is an opaque
    /// handle created by the mixer and controlled by downstream operators that append buffers,
    /// set the gain, or stop playback.
    /// </remarks>
    public unsafe class MixerSourceContext
    {
        const int BlockSize = 256;

        readonly int channelCount;
        readonly bool looping;
        readonly bool removeOnComplete;
        readonly RingList<Mat> sourceBuffers = new();
        readonly WorkQueue<Action> commandQueue = new();
        readonly LinearRamp[] gainRamps;
        readonly float[] gainBuffer;
        readonly BehaviorSubject<MixerSourceState> playbackState;

        int bufferIndex;
        int sampleIndex;
        bool stopping;
        bool playing;
        MixerSourceState currentState;
        MixerSourceState reportedState;

        internal MixerSourceContext(int channelCount, double sampleRate, Mat initialBuffer, bool looping, bool playing, bool removeOnComplete)
        {
            this.channelCount = channelCount;
            this.looping = looping;
            this.playing = playing;
            this.removeOnComplete = removeOnComplete;
            currentState = reportedState = playing ? MixerSourceState.Playing : MixerSourceState.Paused;
            playbackState = new BehaviorSubject<MixerSourceState>(currentState);
            gainRamps = new LinearRamp[channelCount];
            for (int i = 0; i < channelCount; i++)
                gainRamps[i] = new LinearRamp(sampleRate, 1f);
            gainBuffer = new float[channelCount * BlockSize];
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
        /// Sets the gain of every output channel of the source to a new level over the specified
        /// duration.
        /// </summary>
        /// <param name="value">
        /// The gain level, where 1 represents the original signal amplitude.
        /// </param>
        /// <param name="duration">
        /// The length of the gain ramp, in seconds. A duration of zero changes the gain immediately.
        /// </param>
        public void SetGain(float value, double duration)
        {
            commandQueue.Add(() =>
            {
                for (int i = 0; i < gainRamps.Length; i++)
                    gainRamps[i].SetTarget(value, duration);
            });
        }

        /// <summary>
        /// Sets the gain of each output channel of the source to a separate level over the
        /// specified duration.
        /// </summary>
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
        public void SetGain(float[] values, double duration)
        {
            if (values is null)
                throw new ArgumentNullException(nameof(values));

            if (values.Length != channelCount)
                throw new ArgumentException(
                    "The number of gain values must be equal to the number of channels.",
                    nameof(values));

            var channelValues = (float[])values.Clone();
            commandQueue.Add(() =>
            {
                for (int i = 0; i < gainRamps.Length; i++)
                    gainRamps[i].SetTarget(channelValues[i], duration);
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
                for (int i = 0; i < gainRamps.Length; i++)
                    gainRamps[i].SetTarget(0f, fadeDuration);
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

        bool IsGainSettled()
        {
            for (int i = 0; i < gainRamps.Length; i++)
                if (!gainRamps[i].IsCompleted)
                    return false;

            return true;
        }

        internal PaStreamCallbackResult StreamCallback(float* output, uint frameCount, in PaStreamCallbackTimeInfo timeInfo, PaStreamCallbackFlags statusFlags)
        {
            DispatchCommands();

            if (!playing)
            {
                if (stopping)
                    return PaStreamCallbackResult.Complete;

                currentState = MixerSourceState.Paused;
                return PaStreamCallbackResult.Continue;
            }

            int frame = 0;
            while (frame < frameCount)
            {
                if (bufferIndex >= sourceBuffers.Count)
                {
                    if (looping && sourceBuffers.Count > 0)
                    {
                        bufferIndex = 0;
                        sampleIndex = 0;
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
                var count = Math.Min(Math.Min(available, (int)frameCount - frame), BlockSize);
                for (int channel = 0; channel < channelCount; channel++)
                    gainRamps[channel].Generate(gainBuffer.AsSpan(channel * BlockSize, count));

                for (int i = 0; i < count; i++)
                {
                    var sampleColumn = sampleIndex + i;
                    var outputIndex = (frame + i) * channelCount;
                    for (int channel = 0; channel < channelCount; channel++)
                    {
                        var row = bufferRows == 1 ? 0 : channel;
                        output[outputIndex + channel] += gainBuffer[channel * BlockSize + i] * samples[row * stepSamples + sampleColumn];
                    }
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
                bufferIndex = 0;
            }

            var exhausted = bufferIndex >= sourceBuffers.Count && !(looping && sourceBuffers.Count > 0);
            if (stopping)
            {
                if (IsGainSettled() || exhausted)
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

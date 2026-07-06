using System;
using System.Collections.Generic;
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
    /// cursor, scaled by a gain that can vary over time. The source is an opaque handle created
    /// by the mixer and controlled by downstream operators that append buffers, set the gain,
    /// or stop playback.
    /// </remarks>
    public unsafe class MixerSourceContext
    {
        const int BlockSize = 256;

        readonly int channelCount;
        readonly bool looping;
        readonly bool removeOnComplete;
        readonly List<Mat> buffers = new();
        readonly WorkQueue<Action> commandQueue = new();
        readonly LinearRamp gain;
        readonly float[] gainBlock = new float[BlockSize];
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
            gain = new LinearRamp(sampleRate, 1f);
            if (initialBuffer is not null)
            {
                Validate(initialBuffer);
                buffers.Add(initialBuffer);
            }
        }

        void Validate(Mat buffer)
        {
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));

            if (buffer.Rows != channelCount)
                throw new ArgumentException(
                    "The number of rows in the sample buffer must be the same as the number of channels.",
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
        /// for one channel.
        /// </param>
        /// <exception cref="ArgumentNullException">The buffer is null.</exception>
        /// <exception cref="ArgumentException">
        /// The number of rows does not match the number of channels in the stream, the buffer
        /// contains no samples, or the sample depth is not 32-bit floating point.
        /// </exception>
        public void Append(Mat buffer)
        {
            Validate(buffer);
            commandQueue.Add(() => buffers.Add(buffer));
        }

        /// <summary>
        /// Sets the gain of the source to a target level over the specified duration.
        /// </summary>
        /// <param name="target">
        /// The target gain level, where 1 represents the original signal amplitude.
        /// </param>
        /// <param name="duration">
        /// The length of the gain ramp, in seconds. A duration of zero changes the gain immediately.
        /// </param>
        public void SetGain(float target, double duration)
        {
            commandQueue.Add(() => gain.RampTo(target, duration));
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
                gain.RampTo(0f, fadeDuration);
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
                if (bufferIndex >= buffers.Count)
                {
                    if (looping && buffers.Count > 0)
                    {
                        bufferIndex = 0;
                        sampleIndex = 0;
                    }
                    else break;
                }

                var buffer = buffers[bufferIndex];
                buffer.GetRawData(out IntPtr dataPtr, out int step);
                var samples = (float*)dataPtr.ToPointer();
                var stepSamples = step / sizeof(float);
                var bufferColumns = buffer.Cols;

                var available = bufferColumns - sampleIndex;
                var count = Math.Min(Math.Min(available, (int)frameCount - frame), BlockSize);
                gain.Generate(gainBlock.AsSpan(0, count));

                for (int i = 0; i < count; i++)
                {
                    var amplitude = gainBlock[i];
                    var sampleColumn = sampleIndex + i;
                    var outputIndex = (frame + i) * channelCount;
                    for (int channel = 0; channel < channelCount; channel++)
                    {
                        output[outputIndex + channel] += amplitude * samples[channel * stepSamples + sampleColumn];
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

            var exhausted = bufferIndex >= buffers.Count && !(looping && buffers.Count > 0);
            if (stopping)
            {
                if (gain.IsCompleted || exhausted)
                    return PaStreamCallbackResult.Complete;

                currentState = MixerSourceState.Playing;
                return PaStreamCallbackResult.Continue;
            }

            if (removeOnComplete && buffers.Count > 0 && exhausted)
                return PaStreamCallbackResult.Complete;

            currentState = exhausted ? MixerSourceState.Idle : MixerSourceState.Playing;
            return PaStreamCallbackResult.Continue;
        }
    }
}

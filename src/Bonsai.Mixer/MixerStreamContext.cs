using System;
using System.Runtime.InteropServices;
using OpenCV.Net;
using PortAudioNet;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an audio mixer which allows asynchronous queueing of multiple buffers
    /// for simultaneous playback over a single PortAudio output stream.
    /// </summary>
    public unsafe class MixerStreamContext : IDisposable
    {
        private GCHandle handle;
        private readonly PaStream* mixerStream;
        private readonly PaDeviceInfo* selectedDevice;
        private readonly PaStreamParameters streamParameters;
        private readonly WorkQueue<MixerBufferContext> mixerBuffers;

        internal MixerStreamContext(int deviceIndex, double sampleRate, double? suggestedLatency = null)
        {
            handle = GCHandle.Alloc(this);
            selectedDevice = PortAudio.GetDeviceInfo(deviceIndex);
            PaStreamParameters parameters = new()
            {
                device = deviceIndex,
                channelCount = selectedDevice->maxOutputChannels,
                sampleFormat = PaSampleFormat.Float32,
                suggestedLatency = suggestedLatency ?? selectedDevice->defaultLowOutputLatency,
                hostApiSpecificStreamInfo = null,
            };

            PaStream* stream;
            PortAudio.OpenStream
            (
                &stream,
                null,
                &parameters,
                sampleRate,
                PortAudio.FramesPerBufferUnspecified,
                PaStreamFlags.ClipOff,
                StreamCallback,
                (void*)GCHandle.ToIntPtr(handle)
            ).ThrowIfFailure();

            var streamInfo = PortAudio.GetStreamInfo(stream);
            SampleRate = streamInfo->sampleRate;
            OutputLatency = streamInfo->outputLatency;

            mixerStream = stream;
            streamParameters = parameters;
            mixerBuffers = new();
        }

        /// <summary>
        /// Gets the sample rate of the stream, in samples per second.
        /// </summary>
        /// <remarks>
        /// In cases where the hardware sample rate is inaccurate and PortAudio is aware of it,
        /// the value of this field may be different from the sample rate parameter used to
        /// initialize the mixer context.
        /// </remarks>
        public double SampleRate { get; }

        /// <summary>
        /// Gets the output latency of the stream, in seconds.
        /// </summary>
        /// <remarks>
        /// This value provides the most accurate estimate of output latency available to the implementation.
        /// It may differ significantly from the suggested latency value.
        /// </remarks>
        public double OutputLatency { get; }

        /// <summary>
        /// Adds a new buffer to the mixer stream context work queue.
        /// </summary>
        /// <remarks>
        /// At any one moment when the mixer stream context is playing, the data streamed to PortAudio
        /// will be the sum of all queued buffers being played.
        /// </remarks>
        /// <param name="buffer">
        /// A multi-dimensional array containing the sample data, where each row vector represents data from
        /// an individual channel to be mixed into the mixer output stream.
        /// </param>
        /// <exception cref="ArgumentNullException">Buffer is empty.</exception>
        /// <exception cref="ArgumentException">
        /// The number of rows in the buffer does not match the number of channels in the stream.
        /// </exception>
        public void QueueBuffer(Mat buffer)
        {
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));

            if (buffer.Rows != streamParameters.channelCount)
                throw new ArgumentException(
                    "The number of rows in the sample buffer must be the same as the number of channels.",
                    nameof(buffer));

            mixerBuffers.Add(new(buffer));
        }

        /// <summary>
        /// Initiates mixer audio processing.
        /// </summary>
        /// <remarks>
        /// Any pre-queued buffers will begin playing simultaneously when the stream callback
        /// starts requesting data.
        /// </remarks>
        public void Start()
        {
            PortAudio.StartStream(mixerStream).ThrowIfFailure();
        }

        /// <summary>
        /// Stops mixer audio processing.
        /// </summary>
        /// <remarks>
        /// The stream callback will stop requesting data.
        /// </remarks>
        public void Stop()
        {
            PortAudio.StopStream(mixerStream).ThrowIfFailure();
        }

        private PaStreamCallbackResult _StreamCallback(void* input, void* output, uint frameCount, in PaStreamCallbackTimeInfo timeInfo, PaStreamCallbackFlags statusFlags)
        {
            float* outputBuffer = (float*)output;
            PaStreamCallbackTimeInfo localTimeInfo = timeInfo;

            for (int i = 0; i < frameCount; i++)
            {
                for (int channelIndex = 0; channelIndex < streamParameters.channelCount; channelIndex++)
                {
                    outputBuffer[i * streamParameters.channelCount + channelIndex] = 0;
                }
            }

            mixerBuffers.RemoveAll(buffer =>
            {
                var result = buffer.StreamCallback(outputBuffer, frameCount, in localTimeInfo, statusFlags);
                return result != PaStreamCallbackResult.Continue;
            });

            return PaStreamCallbackResult.Continue;
        }

#if NET5_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
        private static PaStreamCallbackResult _StreamCallback(void* input, void* output, uint frameCount, PaStreamCallbackTimeInfo* timeInfo, PaStreamCallbackFlags statusFlags, void* userData)
        {
            GCHandle handle = GCHandle.FromIntPtr((IntPtr)userData);
            MixerStreamContext generator = ((MixerStreamContext)handle.Target!);
            return generator._StreamCallback(input, output, frameCount, in *timeInfo, statusFlags);
        }

        // If you don't need downlevel .NET Framework support, just make _StreamCallback public and use it directly
#if NET5_0_OR_GREATER
    private static readonly delegate* unmanaged[Cdecl]<void*, void*, uint, PaStreamCallbackTimeInfo*, PaStreamCallbackFlags, void*, PaStreamCallbackResult> StreamCallback = &_StreamCallback;
#else
        private static readonly PortAudio.PaStreamCallback ManagedStreamCallback = _StreamCallback;
        private static readonly delegate* unmanaged[Cdecl]<void*, void*, uint, PaStreamCallbackTimeInfo*, PaStreamCallbackFlags, void*, PaStreamCallbackResult> StreamCallback
            = (delegate* unmanaged[Cdecl]<void*, void*, uint, PaStreamCallbackTimeInfo*, PaStreamCallbackFlags, void*, PaStreamCallbackResult>)Marshal.GetFunctionPointerForDelegate(ManagedStreamCallback);
#endif

        /// <summary>
        /// Disposes the mixer context and closes the audio stream.
        /// </summary>
        /// <remarks>
        /// If the audio stream is active, it discards any pending buffers.
        /// </remarks>
        public void Dispose()
        {
            PortAudio.CloseStream(mixerStream);
            mixerBuffers.Clear();
            handle.Free();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{nameof(MixerStreamContext)} {{ " +
                   $"{nameof(SampleRate)} = {SampleRate}, " +
                   $"{nameof(OutputLatency)} = {OutputLatency} }}";
        }
    }
}

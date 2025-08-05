using System;
using System.Runtime.InteropServices;
using OpenCV.Net;
using PortAudioNet;

namespace Bonsai.Mixer
{
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

        public double SampleRate { get; }

        public double OutputLatency { get; }

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

        public void Start()
        {
            PortAudio.StartStream(mixerStream).ThrowIfFailure();
        }

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

        public void Dispose()
        {
            PortAudio.CloseStream(mixerStream);
            mixerBuffers.Clear();
            handle.Free();
        }

        public override string ToString()
        {
            return $"{nameof(MixerStreamContext)} {{ " +
                   $"{nameof(SampleRate)} = {SampleRate}, " +
                   $"{nameof(OutputLatency)} = {OutputLatency} }}";
        }
    }
}

using System;
using PortAudioNet;

namespace Bonsai.Mixer
{
    internal class MixerBufferContext
    {
        int sampleIndex;

        public MixerBufferContext(float[] samples, float[] channelScale)
        {
            Samples = samples ?? throw new ArgumentNullException(nameof(samples));
            ChannelScale = channelScale ?? throw new ArgumentNullException(nameof(channelScale));
        }

        public float[] Samples { get; }

        public float[] ChannelScale { get; }

        public unsafe PaStreamCallbackResult StreamCallback(float* outputBuffer, uint frameCount, in PaStreamCallbackTimeInfo timeInfo, PaStreamCallbackFlags statusFlags)
        {
            for (int i = 0; i < frameCount && sampleIndex < Samples.Length; i++, sampleIndex++)
            {
                for (int channelIndex = 0; channelIndex < ChannelScale.Length; channelIndex++)
                {
                    outputBuffer[i * ChannelScale.Length + channelIndex] += Samples[sampleIndex] * ChannelScale[channelIndex];
                }
            }

            return sampleIndex < Samples.Length ? PaStreamCallbackResult.Continue : PaStreamCallbackResult.Complete;
        }
    }
}

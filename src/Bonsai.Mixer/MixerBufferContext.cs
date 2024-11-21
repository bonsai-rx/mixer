using System;
using OpenCV.Net;
using PortAudioNet;

namespace Bonsai.Mixer
{
    internal class MixerBufferContext
    {
        int sampleIndex;

        public MixerBufferContext(Mat buffer)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            if (buffer.Depth != Depth.F32)
            {
                throw new ArgumentException(
                    $"Invalid sample depth '{buffer.Depth}'. All samples must be {Depth.F32}",
                    nameof(buffer));
            }
        }

        public Mat Buffer { get; }

        public unsafe PaStreamCallbackResult StreamCallback(float* outputBuffer, uint frameCount, in PaStreamCallbackTimeInfo timeInfo, PaStreamCallbackFlags statusFlags)
        {
            var bufferCols = Buffer.Cols;
            var bufferRows = Buffer.Rows;
            var numSamples = Math.Min(frameCount, bufferCols - sampleIndex);
            Buffer.GetRawData(out IntPtr dataPtr, out int step);
            var sampleBuffer = (float*)dataPtr.ToPointer();
            var stepSamples = step / sizeof(float);

            for (int i = 0; i < numSamples; i++)
            {
                for (int channelIndex = 0; channelIndex < bufferRows; channelIndex++)
                {
                    outputBuffer[i * bufferRows + channelIndex] += sampleBuffer[channelIndex * stepSamples + sampleIndex + i];
                }
            }
            sampleIndex += (int)numSamples;
            return sampleIndex < bufferCols ? PaStreamCallbackResult.Continue : PaStreamCallbackResult.Complete;
        }
    }
}

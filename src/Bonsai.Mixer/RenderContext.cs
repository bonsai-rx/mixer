namespace Bonsai.Mixer
{
    internal readonly unsafe struct RenderContext(float* sourceGainBuffer, float* channelGainBuffer)
    {
        public const int BlockSize = 256;
        public readonly float* SourceGainBuffer = sourceGainBuffer;
        public readonly float* ChannelGainBuffer = channelGainBuffer;
    }
}

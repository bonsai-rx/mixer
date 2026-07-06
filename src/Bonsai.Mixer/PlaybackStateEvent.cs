namespace Bonsai.Mixer
{
    /// <summary>
    /// A source playback-state transition handed from the audio callback thread to the
    /// notification pump.
    /// </summary>
    internal readonly struct PlaybackStateEvent(MixerSourceContext source, MixerSourceState state)
    {
        public readonly MixerSourceContext Source = source;
        public readonly MixerSourceState State = state;
    }
}

namespace Bonsai.Mixer
{
    /// <summary>
    /// Specifies the playback state of a mixer source.
    /// </summary>
    public enum MixerSourceState
    {
        /// <summary>
        /// The source is playing, advancing its playback cursor.
        /// </summary>
        Playing,

        /// <summary>
        /// The source is paused, holding its playback cursor.
        /// </summary>
        Paused
    }
}

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
        Paused,

        /// <summary>
        /// The source is playing but has no more samples to play.
        /// </summary>
        Idle,

        /// <summary>
        /// The source has been removed from the mixer.
        /// </summary>
        Stopped
    }
}

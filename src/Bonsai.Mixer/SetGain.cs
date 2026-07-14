using System;
using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that sets the source gain of every mixer source in the sequence to a
    /// target level over a specified duration.
    /// </summary>
    /// <remarks>
    /// The source gain scales every output channel uniformly and is independent of the per-channel
    /// gain set by <see cref="SetChannelGain"/>. The effective gain applied to a channel is the
    /// product of the source gain and the per-channel gain for that channel.
    /// </remarks>
    [Description("Sets the source gain of every mixer source in the sequence to a target level over a specified duration.")]
    public class SetGain : Sink<MixerSourceContext>
    {
        /// <summary>
        /// Gets or sets the target source gain level, where 1 represents the original signal amplitude.
        /// </summary>
        [Description("The target source gain level, where 1 represents the original signal amplitude.")]
        public float Gain { get; set; } = 1;

        /// <summary>
        /// Gets or sets the duration of the gain ramp, in seconds.
        /// </summary>
        /// <remarks>
        /// The default of 20 milliseconds keeps the gain change smooth, since very short ramps can
        /// produce audible clipping. A duration of zero changes the gain immediately.
        /// </remarks>
        [Description("The duration of the gain ramp, in seconds. A value of zero changes the gain immediately.")]
        public double Duration { get; set; } = 0.02;

        /// <summary>
        /// Sets the source gain of every mixer source in an observable sequence to the target level
        /// over the specified duration.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="MixerSourceContext"/> objects whose gain is set.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/> sequence
        /// but where there is an additional side effect of setting the source gain of every mixer
        /// source in the sequence.
        /// </returns>
        public override IObservable<MixerSourceContext> Process(IObservable<MixerSourceContext> source)
        {
            return source.Do(mixerSource => mixerSource.SetGain(Gain, Duration));
        }
    }
}

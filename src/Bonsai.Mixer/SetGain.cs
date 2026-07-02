using System;
using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that sets the gain of every mixer source in the sequence to a
    /// target level over a specified duration.
    /// </summary>
    [Description("Sets the gain of every mixer source in the sequence to a target level over a specified duration.")]
    public class SetGain : Sink<MixerSourceContext>
    {
        /// <summary>
        /// Gets or sets the target gain level, where 1 represents the original signal amplitude.
        /// </summary>
        [Description("The target gain level, where 1 represents the original signal amplitude.")]
        public double Gain { get; set; } = 1;

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
        /// Sets the gain of every mixer source in an observable sequence to the target level over
        /// the specified duration.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="MixerSourceContext"/> objects whose gain is set.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/> sequence
        /// but where there is an additional side effect of setting the gain of every mixer source
        /// in the sequence.
        /// </returns>
        public override IObservable<MixerSourceContext> Process(IObservable<MixerSourceContext> source)
        {
            return source.Do(mixerSource => mixerSource.SetGain((float)Gain, Duration));
        }
    }
}

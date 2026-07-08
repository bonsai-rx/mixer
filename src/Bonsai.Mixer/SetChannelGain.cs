using System;
using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that sets the per-channel gain of every mixer source in the sequence
    /// to a set of target levels over a specified duration.
    /// </summary>
    [Description("Sets the per-channel gain of every mixer source in the sequence to a set of target levels over a specified duration.")]
    public class SetChannelGain : Sink<MixerSourceContext>
    {
        /// <summary>
        /// Gets or sets the target gain level for each output channel, where 1 represents the
        /// original signal amplitude.
        /// </summary>
        [TypeConverter(typeof(UnidimensionalArrayConverter))]
        [Description("The target gain level for each output channel, where 1 represents the original signal amplitude.")]
        public double[] Gain { get; set; }

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
        /// Sets the per-channel gain of every mixer source in an observable sequence to the target
        /// levels over the specified duration.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="MixerSourceContext"/> objects whose per-channel gain is set.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/> sequence
        /// but where there is an additional side effect of setting the per-channel gain of every
        /// mixer source in the sequence.
        /// </returns>
        public override IObservable<MixerSourceContext> Process(IObservable<MixerSourceContext> source)
        {
            return source.Do(mixerSource =>
            {
                var gain = Gain;
                if (gain is null)
                    throw new InvalidOperationException("The Gain property must specify a target level for each output channel.");

                var targets = new float[gain.Length];
                for (int i = 0; i < gain.Length; i++)
                    targets[i] = (float)gain[i];

                mixerSource.SetGain(targets, Duration);
            });
        }
    }
}

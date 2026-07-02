using System;
using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that stops playback of every mixer source in the sequence, fading
    /// to silence over a specified duration before each source is removed.
    /// </summary>
    [Description("Stops playback of every mixer source in the sequence, fading to silence over a specified duration before each source is removed.")]
    public class StopSource : Sink<MixerSourceContext>
    {
        /// <summary>
        /// Gets or sets the duration of the fade-out, in seconds.
        /// </summary>
        /// <remarks>
        /// The default of 20 milliseconds keeps the stop smooth, since very short fades can
        /// produce audible clipping. A duration of zero stops playback immediately.
        /// </remarks>
        [Description("The duration of the fade-out, in seconds. A value of zero stops playback immediately.")]
        public double FadeDuration { get; set; } = 0.02;

        /// <summary>
        /// Stops playback of every mixer source in an observable sequence, fading to silence over
        /// the specified duration before each source is removed.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="MixerSourceContext"/> objects whose playback is stopped.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/> sequence
        /// but where there is an additional side effect of stopping playback of every mixer
        /// source in the sequence.
        /// </returns>
        public override IObservable<MixerSourceContext> Process(IObservable<MixerSourceContext> source)
        {
            return source.Do(mixerSource => mixerSource.Stop(FadeDuration));
        }
    }
}

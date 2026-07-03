using System;
using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that resumes playback of every mixer source in the sequence from
    /// its current position.
    /// </summary>
    [Description("Resumes playback of every mixer source in the sequence from its current position.")]
    public class PlaySource : Sink<MixerSourceContext>
    {
        /// <summary>
        /// Resumes playback of every mixer source in an observable sequence from its current
        /// position.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="MixerSourceContext"/> objects whose playback is resumed.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/> sequence
        /// but where there is an additional side effect of resuming playback of every mixer
        /// source in the sequence.
        /// </returns>
        public override IObservable<MixerSourceContext> Process(IObservable<MixerSourceContext> source)
        {
            return source.Do(mixerSource => mixerSource.Play());
        }
    }
}

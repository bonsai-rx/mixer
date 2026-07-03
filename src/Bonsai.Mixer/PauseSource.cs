using System;
using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that pauses playback of every mixer source in the sequence,
    /// holding its position until playback is resumed.
    /// </summary>
    [Description("Pauses playback of every mixer source in the sequence, holding its position until playback is resumed.")]
    public class PauseSource : Sink<MixerSourceContext>
    {
        /// <summary>
        /// Pauses playback of every mixer source in an observable sequence, holding its position
        /// until playback is resumed.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="MixerSourceContext"/> objects whose playback is paused.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/> sequence
        /// but where there is an additional side effect of pausing playback of every mixer
        /// source in the sequence.
        /// </returns>
        public override IObservable<MixerSourceContext> Process(IObservable<MixerSourceContext> source)
        {
            return source.Do(mixerSource => mixerSource.Pause());
        }
    }
}

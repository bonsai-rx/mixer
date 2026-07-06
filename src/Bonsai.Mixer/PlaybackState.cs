using System;
using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that reports the playback state of each mixer source in the
    /// sequence, emitting its current state and every subsequent transition.
    /// </summary>
    [Description("Reports the playback state of each mixer source in the sequence, emitting its current state and every subsequent transition.")]
    public class PlaybackState : Combinator<MixerSourceContext, MixerSourceState>
    {
        /// <summary>
        /// Reports the playback state of each mixer source in an observable sequence, emitting its
        /// current state and every subsequent transition.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="MixerSourceContext"/> objects whose playback state is reported.
        /// </param>
        /// <returns>
        /// An observable sequence of <see cref="MixerSourceState"/> values, starting with the
        /// current state of each source and emitting each transition, completing when the source is
        /// removed from the mixer.
        /// </returns>
        public override IObservable<MixerSourceState> Process(IObservable<MixerSourceContext> source)
        {
            return source.SelectMany(mixerSource => mixerSource.PlaybackState);
        }
    }
}

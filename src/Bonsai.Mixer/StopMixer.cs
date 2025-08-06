using System;
using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that stops audio processing on every mixer stream
    /// context in the sequence.
    /// </summary>
    [Description("Stops audio processing on every mixer stream context in the sequence.")]
    public class StopMixer : Sink<MixerStreamContext>
    {
        /// <summary>
        /// Stops audio processing on every mixer stream context in an observable sequence.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="MixerStreamContext"/> objects.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/>
        /// sequence but where there is an additional side effect of stopping
        /// audio processing on every mixer stream context in the sequence.
        /// </returns>
        public override IObservable<MixerStreamContext> Process(IObservable<MixerStreamContext> source)
        {
            return source.Do(mixer => mixer.Stop());
        }
    }
}

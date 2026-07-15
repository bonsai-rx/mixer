using System;
using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that initiates audio processing on every mixer context
    /// in the sequence.
    /// </summary>
    [Description("Initiates audio processing on every mixer context in the sequence.")]
    public class StartMixer : Sink<MixerContext>
    {
        /// <summary>
        /// Initiates audio processing on every mixer context in an observable sequence.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="MixerContext"/> objects.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/>
        /// sequence but where there is an additional side effect of initiating
        /// audio processing on every mixer context in the sequence.
        /// </returns>
        public override IObservable<MixerContext> Process(IObservable<MixerContext> source)
        {
            return source.Do(mixer => mixer.Start());
        }
    }
}

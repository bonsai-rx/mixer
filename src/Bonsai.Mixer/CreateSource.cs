using System;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that creates a new mixer source on every mixer stream context in
    /// the sequence.
    /// </summary>
    [Combinator]
    [Description("Creates a new mixer source on every mixer stream context in the sequence.")]
    public class CreateSource
    {
        /// <summary>
        /// Gets or sets a value specifying whether the source loops its playback queue continuously.
        /// </summary>
        [Description("True to loop the playback queue continuously; otherwise playback stops once all queued buffers have played.")]
        public bool Loop { get; set; }

        /// <summary>
        /// Creates a new mixer source on every mixer stream context in an observable sequence.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="MixerStreamContext"/> objects on which to create the audio source.
        /// </param>
        /// <returns>
        /// An observable sequence of <see cref="MixerSourceContext"/> objects, one created on each
        /// mixer stream context in the <paramref name="source"/> sequence, used to control playback.
        /// </returns>
        public IObservable<MixerSourceContext> Process(IObservable<MixerStreamContext> source)
        {
            return source.SelectMany(mixer => Observable.Create<MixerSourceContext>(observer =>
            {
                var context = mixer.CreateSource(Loop);
                observer.OnNext(context);
                return Disposable.Create(() => context.Stop(0));
            }));
        }
    }
}

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
        /// <remarks>
        /// True to loop the playback queue continuously; otherwise playback stops once all queued
        /// buffers have played.
        /// </remarks>
        [Description("True to loop the playback queue continuously; otherwise playback stops once all queued buffers have played.")]
        public bool Looping { get; set; }

        /// <summary>
        /// Gets or sets a value specifying whether the source starts playing immediately.
        /// </summary>
        /// <remarks>
        /// True to start the source playing immediately; otherwise the source starts paused.
        /// </remarks>
        [Description("True to start the source playing immediately; otherwise the source starts paused.")]
        public bool Playing { get; set; } = true;

        /// <summary>
        /// Gets or sets the initial source gain, where 1 represents the original signal amplitude.
        /// </summary>
        /// <remarks>
        /// The source starts at this gain rather than the default unity, so it can begin silent
        /// for a clean fade-in, or at a chosen level.
        /// </remarks>
        [Description("The initial source gain, where 1 represents the original signal amplitude.")]
        public float Gain { get; set; } = 1;

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
                var context = mixer.CreateSource(Looping, Playing, Gain);
                observer.OnNext(context);
                return Disposable.Create(() => context.Stop(0));
            }));
        }
    }
}

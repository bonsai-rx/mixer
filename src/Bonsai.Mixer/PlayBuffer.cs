using System;
using System.ComponentModel;
using System.Reactive.Linq;
using OpenCV.Net;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that plays each audio buffer from the first sequence as a one-shot
    /// source on every mixer context in the second sequence.
    /// </summary>
    [Combinator]
    [Description("Plays each audio buffer from the first sequence as a one-shot source on every mixer context in the second sequence.")]
    public class PlayBuffer
    {
        /// <summary>
        /// Plays each audio buffer in an observable sequence as a one-shot source on every mixer
        /// context in a second sequence.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="Mat"/> objects representing the audio buffers to play.
        /// </param>
        /// <param name="mixer">
        /// A sequence of <see cref="MixerContext"/> objects on which to play the audio buffers.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/> sequence
        /// but where there is an additional side effect of playing each audio buffer as a one-shot
        /// source on every mixer context in the <paramref name="mixer"/> sequence.
        /// </returns>
        public IObservable<Mat> Process(IObservable<Mat> source, IObservable<MixerContext> mixer)
        {
            return mixer.SelectMany(context => source.Do(context.PlayBuffer));
        }
    }
}

using System;
using System.ComponentModel;
using System.Reactive.Linq;
using OpenCV.Net;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that appends the audio buffers from the first sequence to the
    /// playback queue of every mixer source in the second sequence.
    /// </summary>
    [Combinator]
    [Description("Appends the audio buffers from the first sequence to the playback queue of every mixer source in the second sequence.")]
    public class AppendBuffer
    {
        /// <summary>
        /// Appends the audio buffers in an observable sequence to the playback queue of every
        /// mixer source in a second sequence.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="Mat"/> objects representing the audio buffers to append.
        /// </param>
        /// <param name="mixerSource">
        /// A sequence of <see cref="MixerSourceContext"/> objects to whose playback queue the
        /// audio buffers are appended.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/> sequence
        /// but where there is an additional side effect of appending the audio buffers to the
        /// playback queue of every mixer source in the <paramref name="mixerSource"/> sequence.
        /// </returns>
        public IObservable<Mat> Process(IObservable<Mat> source, IObservable<MixerSourceContext> mixerSource)
        {
            return mixerSource.SelectMany(context => source.Do(context.Append));
        }
    }
}

using System;
using System.ComponentModel;
using System.Reactive.Linq;
using OpenCV.Net;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that replaces the playback queue of every mixer source in the
    /// second sequence with each audio buffer from the first sequence, fading out the queued
    /// audio over a specified duration.
    /// </summary>
    /// <remarks>
    /// The queued audio fades out before the new buffer starts, as in <see cref="ClearBuffers"/>,
    /// and the two never overlap. An optional gap of silence can be held between the end of the
    /// fade and the start of the new buffer.
    /// </remarks>
    [Combinator]
    [Description("Replaces the playback queue of every mixer source in the second sequence with each audio buffer from the first sequence, fading out the queued audio over a specified duration.")]
    public class ReplaceBuffer
    {
        /// <summary>
        /// Gets or sets the duration of the fade-out, in seconds.
        /// </summary>
        /// <remarks>
        /// The default of 20 milliseconds keeps the cut smooth, since discarding audio mid-waveform
        /// can produce an audible click. A duration of zero discards the queue immediately.
        /// </remarks>
        [Description("The duration of the fade-out, in seconds. A value of zero discards the queue immediately.")]
        public double FadeDuration { get; set; } = 0.02;

        /// <summary>
        /// Gets or sets the duration of the silence between the end of the fade-out and the start
        /// of the new buffer, in seconds.
        /// </summary>
        /// <remarks>
        /// The gap is measured in samples from the end of the fade, or from the moment the queued
        /// audio ran out if that came first. It is played only once, even on a looping source.
        /// The default of zero starts the new buffer as soon as the fade ends.
        /// </remarks>
        [Description("The duration of the silence between the end of the fade-out and the start of the new buffer, in seconds. A value of zero starts the new buffer as soon as the fade ends.")]
        public double GapDuration { get; set; }

        /// <summary>
        /// Replaces the playback queue of every mixer source in an observable sequence with each
        /// audio buffer from a second sequence, fading out the queued audio over the specified
        /// duration and holding the specified gap of silence before the new buffer starts.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="Mat"/> objects representing the audio buffers that replace
        /// the playback queue.
        /// </param>
        /// <param name="mixerSource">
        /// A sequence of <see cref="MixerSourceContext"/> objects whose playback queue is
        /// replaced by each audio buffer.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/> sequence
        /// but where there is an additional side effect of replacing the playback queue of every
        /// mixer source in the <paramref name="mixerSource"/> sequence.
        /// </returns>
        public IObservable<Mat> Process(IObservable<Mat> source, IObservable<MixerSourceContext> mixerSource)
        {
            return mixerSource.SelectMany(context => source.Do(buffer => context.Replace(buffer, FadeDuration, GapDuration)));
        }
    }
}

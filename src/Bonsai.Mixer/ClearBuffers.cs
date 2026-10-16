using System;
using System.ComponentModel;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that clears the playback queue of every mixer source in the
    /// sequence, fading the queued audio to silence over a specified duration.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="StopSource"/>, the source stays on the mixer and keeps playing any
    /// buffers appended after the clear.
    /// </remarks>
    [Description("Clears the playback queue of every mixer source in the sequence, fading the queued audio to silence over a specified duration.")]
    public class ClearBuffers : Sink<MixerSourceContext>
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
        /// Clears the playback queue of every mixer source in an observable sequence, fading the
        /// queued audio to silence over the specified duration.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="MixerSourceContext"/> objects whose playback queue is cleared.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/> sequence
        /// but where there is an additional side effect of clearing the playback queue of every
        /// mixer source in the sequence.
        /// </returns>
        public override IObservable<MixerSourceContext> Process(IObservable<MixerSourceContext> source)
        {
            return source.Do(mixerSource => mixerSource.Clear(FadeDuration));
        }
    }
}

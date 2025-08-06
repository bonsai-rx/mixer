using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Xml.Serialization;
using OpenCV.Net;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that adds audio buffers in the sequence to the work queue
    /// of a mixer stream context.
    /// </summary>
    [ResetCombinator]
    [Description("Adds audio buffers in the sequence to the work queue of a mixer stream context.")]
    public class QueueBuffer : Sink<Mat>
    {
        /// <summary>
        /// Gets or sets the mixer stream context on which to queue the audio buffer.
        /// </summary>
        /// <remarks>
        /// This property must be mapped dynamically.
        /// </remarks>
        [XmlIgnore]
        [Description("The mixer stream context on which to queue the audio buffer.")]
        public MixerStreamContext Mixer { get; set; }

        /// <summary>
        /// Adds audio buffers in an observable sequence to the work queue of the specified
        /// mixer stream context.
        /// </summary>
        /// <param name="source">
        /// A sequence of <see cref="Mat"/> objects representing audio buffers to
        /// be queued in the mixer stream context.
        /// </param>
        /// <returns>
        /// An observable sequence that is identical to the <paramref name="source"/>
        /// sequence but where there is an additional side effect of queueing the
        /// audio buffers in the sequence into the specified mixer stream context.
        /// </returns>
        public override IObservable<Mat> Process(IObservable<Mat> source)
        {
            return source.Do(buffer =>
            {
                Mixer?.QueueBuffer(buffer);
            });
        }
    }
}

using System;
using System.Reactive.Linq;
using System.Xml.Serialization;
using OpenCV.Net;

namespace Bonsai.Mixer
{
    [ResetCombinator]
    public class QueueBuffer : Sink<Mat>
    {
        [XmlIgnore]
        public MixerStreamContext Mixer { get; set; }

        public override IObservable<Mat> Process(IObservable<Mat> source)
        {
            return source.Do(buffer =>
            {
                Mixer?.QueueBuffer(buffer);
            });
        }
    }
}

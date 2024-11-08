using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Xml.Serialization;

namespace Bonsai.Mixer
{
    [ResetCombinator]
    public class QueueBuffer : Sink<float[]>
    {
        [TypeConverter(typeof(UnidimensionalArrayConverter))]
        public float[] ChannelScale { get; set; }

        [XmlIgnore]
        public MixerStreamContext Mixer { get; set; }

        public override IObservable<float[]> Process(IObservable<float[]> source)
        {
            return source.Do(buffer =>
            {
                Mixer?.QueueBuffer(buffer, ChannelScale);
            });
        }
    }
}

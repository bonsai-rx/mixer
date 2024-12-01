using System;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    public class StartMixer : Sink<MixerStreamContext>
    {
        public override IObservable<MixerStreamContext> Process(IObservable<MixerStreamContext> source)
        {
            return source.Do(mixer => mixer.Start());
        }
    }
}

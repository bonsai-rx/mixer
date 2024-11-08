using System;
using System.Reactive.Linq;

namespace Bonsai.Mixer
{
    public class StopMixer : Sink<MixerStreamContext>
    {
        public override IObservable<MixerStreamContext> Process(IObservable<MixerStreamContext> source)
        {
            return source.Do(mixer => mixer.Stop());
        }
    }
}

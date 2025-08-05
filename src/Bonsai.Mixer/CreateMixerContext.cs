using System;
using System.ComponentModel;
using System.Reactive.Linq;
using PortAudioNet;
using System.Reactive.Disposables;

namespace Bonsai.Mixer
{
    [Description("")]
    [Combinator(MethodName = nameof(Generate))]
    [WorkflowElementCategory(ElementCategory.Source)]
    public class CreateMixerContext
    {
        [TypeConverter(typeof(HostApiConverter))]
        public string HostApi { get; set; }

        [TypeConverter(typeof(DeviceNameConverter))]
        public string DeviceName { get; set; }

        public double SampleRate { get; set; } = 48e3;

        public double? SuggestedLatency { get; set; }

        public unsafe IObservable<MixerStreamContext> Generate()
        {
            return Observable.Create<MixerStreamContext>(observer =>
            {
                var engine = PortAudioEngine.Initialize();
                int hostApiCount = PortAudio.GetHostApiCount();
                PortAudio.CheckReturn(hostApiCount);

                int selectedIndex = PortAudio.GetDefaultOutputDevice();
                for (int hostIndex = 0; hostIndex < hostApiCount; hostIndex++)
                {
                    PaHostApiInfo* hostApi = PortAudio.GetHostApiInfo(hostIndex);
                    string hostApiName = PortAudio.PtrToString(hostApi->name);
                    if (hostApiName == HostApi)
                    {
                        for (int hostDeviceIndex = 0; hostDeviceIndex < hostApi->deviceCount; hostDeviceIndex++)
                        {
                            int deviceIndex = PortAudio.HostApiDeviceIndexToDeviceIndex(hostIndex, hostDeviceIndex);
                            PortAudio.CheckReturn(deviceIndex);
                            PaDeviceInfo* device = PortAudio.GetDeviceInfo(deviceIndex);
                            if (device->maxOutputChannels > 0)
                            {
                                var deviceName = PortAudio.PtrToString(device->name);
                                if (deviceName == DeviceName)
                                    selectedIndex = deviceIndex;
                            }
                        }
                    }
                }

                var mixerStream = new MixerStreamContext(selectedIndex, SampleRate, SuggestedLatency);
                observer.OnNext(mixerStream);
                return Disposable.Create(() =>
                {
                    mixerStream.Dispose();
                    engine.Dispose();
                });
            });
        }
    }
}

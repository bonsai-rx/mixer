using System.Collections.Generic;
using System.ComponentModel;
using PortAudioNet;

namespace Bonsai.Mixer
{
    internal class DeviceNameConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override unsafe StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var instance = (CreateMixerContext)context.Instance;
            if (instance is null)
                return base.GetStandardValues(context);

            PortAudio.Initialize().ThrowIfFailure();
            int hostApiCount = PortAudio.GetHostApiCount();
            PortAudio.CheckReturn(hostApiCount);

            List<string> deviceNames = new();
            for (int hostIndex = 0; hostIndex < hostApiCount; hostIndex++)
            {
                PaHostApiInfo* hostApi = PortAudio.GetHostApiInfo(hostIndex);
                string hostApiName = PortAudio.PtrToString(hostApi->name);
                if (hostApiName == instance.HostApi)
                {
                    for (int hostDeviceIndex = 0; hostDeviceIndex < hostApi->deviceCount; hostDeviceIndex++)
                    {
                        int deviceIndex = PortAudio.HostApiDeviceIndexToDeviceIndex(hostIndex, hostDeviceIndex);
                        PortAudio.CheckReturn(deviceIndex);
                        PaDeviceInfo* device = PortAudio.GetDeviceInfo(deviceIndex);
                        if (device->maxOutputChannels > 0)
                        {
                            deviceNames.Add(PortAudio.PtrToString(device->name));
                        }
                    }
                }
            }

            return new StandardValuesCollection(deviceNames);
        }
    }
}

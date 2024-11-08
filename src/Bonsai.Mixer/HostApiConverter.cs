using System.Collections.Generic;
using System.ComponentModel;
using PortAudioNet;

namespace Bonsai.Mixer
{
    internal class HostApiConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override unsafe StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            using var engine = PortAudioEngine.Initialize();
            int hostApiCount = PortAudio.GetHostApiCount();
            PortAudio.CheckReturn(hostApiCount);

            List<string> hostApiNames = new();
            for (int hostIndex = 0; hostIndex < hostApiCount; hostIndex++)
            {
                PaHostApiInfo* hostApi = PortAudio.GetHostApiInfo(hostIndex);
                hostApiNames.Add(PortAudio.PtrToString(hostApi->name));
            }

            return new StandardValuesCollection(hostApiNames);
        }
    }
}

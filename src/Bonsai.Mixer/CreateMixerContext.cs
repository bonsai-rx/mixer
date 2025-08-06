using System;
using System.ComponentModel;
using System.Reactive.Linq;
using PortAudioNet;
using System.Reactive.Disposables;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents an operator that creates a new mixer stream context used to control
    /// simultaneous playback of multiple audio buffers.
    /// </summary>
    [Description("Creates a new mixer stream context used to control simultaneous playback of multiple audio buffers.")]
    [Combinator(MethodName = nameof(Generate))]
    [WorkflowElementCategory(ElementCategory.Source)]
    public class CreateMixerContext
    {
        /// <summary>
        /// Gets or sets the name of the host API that should be used to implement low-level audio processing.
        /// </summary>
        /// <remarks>
        /// If neither <see cref="HostApi"/> nor <see cref="DeviceName"/> are specified, the
        /// default output device will be selected.
        /// </remarks>
        [TypeConverter(typeof(HostApiConverter))]
        [Description("The name of the host API that should be used to implement low-level audio processing.")]
        public string HostApi { get; set; }

        /// <summary>
        /// Gets or sets the name of the device that should be used to implement low-level audio processing.
        /// </summary>
        /// <remarks>
        /// If neither <see cref="HostApi"/> nor <see cref="DeviceName"/> are specified, the
        /// default output device will be selected.
        /// </remarks>
        [TypeConverter(typeof(DeviceNameConverter))]
        [Description("The name of the device that should be used to implement low-level audio processing.")]
        public string DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the desired sample rate of the output stream, in samples per second.
        /// </summary>
        [Description("The desired sample rate of the output stream, in samples per second.")]
        public double SampleRate { get; set; } = 48e3;

        /// <summary>
        /// Gets or sets the desired output latency, in seconds.
        /// </summary>
        /// <remarks>
        /// If not specified, the default low output latency value for the selected device will be
        /// used. Setting this value low may not guarantee workable playback. Testing and benchmarking
        /// are always recommended to assess the minimum required latency.
        /// </remarks>
        [Description("The desired output latency, in seconds.")]
        public double? SuggestedLatency { get; set; }

        /// <summary>
        /// Generates an observable sequence that initializes and returns a new mixer stream context
        /// object which can be used to control simultaneous playback of multiple audio buffers.
        /// </summary>
        /// <returns>
        /// A sequence containing the <see cref="MixerStreamContext"/> object.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// The output sequence will emit an error if the specified target device or host API
        /// cannot be found.
        /// </exception>
        public unsafe IObservable<MixerStreamContext> Generate()
        {
            return Observable.Create<MixerStreamContext>(observer =>
            {
                var targetHostApi = HostApi;
                var targetDeviceName = DeviceName;
                var engine = PortAudioEngine.Initialize();
                int hostApiCount = PortAudio.GetHostApiCount();
                PortAudio.CheckReturn(hostApiCount);

                int selectedIndex = -1;
                for (int hostIndex = 0; hostIndex < hostApiCount; hostIndex++)
                {
                    PaHostApiInfo* hostApi = PortAudio.GetHostApiInfo(hostIndex);
                    string hostApiName = PortAudio.PtrToString(hostApi->name);
                    if (hostApiName == targetHostApi)
                    {
                        for (int hostDeviceIndex = 0; hostDeviceIndex < hostApi->deviceCount; hostDeviceIndex++)
                        {
                            int deviceIndex = PortAudio.HostApiDeviceIndexToDeviceIndex(hostIndex, hostDeviceIndex);
                            PortAudio.CheckReturn(deviceIndex);
                            PaDeviceInfo* device = PortAudio.GetDeviceInfo(deviceIndex);
                            if (device->maxOutputChannels > 0)
                            {
                                var deviceName = PortAudio.PtrToString(device->name);
                                if (deviceName == targetDeviceName)
                                    selectedIndex = deviceIndex;
                            }
                        }
                    }
                }

                if (selectedIndex < 0)
                    if (string.IsNullOrEmpty(targetHostApi) && string.IsNullOrEmpty(targetDeviceName))
                        selectedIndex = PortAudio.GetDefaultOutputDevice();
                    else if (string.IsNullOrEmpty(targetDeviceName))
                        throw new InvalidOperationException("Device must be specified when selecting a host api.");
                    else if (string.IsNullOrEmpty(targetHostApi))
                        throw new InvalidOperationException("Host api must be specified when selecting a device name.");
                    else
                        throw new InvalidOperationException(
                            $"Device '{targetDeviceName}' could not be found in '{targetHostApi}'.");

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

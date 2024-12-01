using System;
using System.Reactive.Disposables;
using PortAudioNet;

namespace Bonsai.Mixer
{
    static class PortAudioEngine
    {
        public static IDisposable Initialize()
        {
            PortAudio.Initialize().ThrowIfFailure();
            return Disposable.Create(() => PortAudio.Terminate().ThrowIfFailure());
        }
    }
}

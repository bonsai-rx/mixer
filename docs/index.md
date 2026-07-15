# Getting started

`Bonsai.Mixer` provides low-latency, multi-channel audio playback over [PortAudio](https://www.portaudio.com/). It plays audio as one-shot sounds or as long-lived sources that can loop, pause, stop, and move across the speakers while they play. This guide covers how the package fits together and walks through playing a sound.

## Overview

The package is organized around three core concepts:

- **The mixer context** is an output stream on a device. [`CreateMixerContext`] opens it and [`StartMixer`] starts its real-time audio callback. The device, sample rate, channel count, and latency are chosen at creation, and the stream reports the actual values it opened with.
- **Audio buffers** are the audio content, held as [`Mat`] arrays of 32-bit floating-point samples with one row per output channel, or a single row for a mono sound that the mixer broadcasts across the channels. They are generated or loaded with the usual `Bonsai.Dsp` operators, off the audio thread.
- **Sources** are the addressable voices that play buffers. [`CreateSource`] emits a handle for appending buffers with [`AppendBuffer`], setting the gain with [`SetGain`], and looping, pausing, or stopping playback.

[`PlayBuffer`] skips the handle entirely, playing a buffer directly on the mixer as a one-shot source managed internally.

The real-time audio callback only sums the active sources and applies gain, leaving all synthesis and processing to the upstream workflow. See [how the mixer works](xref:audio-model) for what this means for latency and logging.

## Playing a sound

Below is a small workflow to open a mixer, start it, and play a single buffer.

:::workflow
![Playing a buffer](~/workflows/getting-started.bonsai)
:::

- [`CreateMixerContext`] opens the output stream. Left at its defaults it uses the default device and its maximum channel count.
- [`StartMixer`] begins requesting audio from the device.
- Publishing the started context in a `BehaviorSubject` named `Mixer` lets every downstream branch reach the same stream.
- A `Bonsai.Dsp` generator produces a buffer of `F32` samples, which [`PlayBuffer`] plays once on the mixer.

> [!TIP]
> Derive the sample rate from the mixer context rather than hardcoding it. Map `SampleRate` from the context onto the generator with a `MemberSelector` and a `PropertyMapping` so the generated buffer always matches the open stream.

## Where to go next

- [Audio sources](xref:sources): create controllable sources, loop them, and append buffers over time.
- [Playback control](xref:playback-control): play, pause, and stop sources, and observe their state.
- [Gain control](xref:gain-control): set volume and position a source across the output channels.
- [How the mixer works](xref:audio-model): the audio callback model, buffer format, and what to keep off the audio thread.

[`CreateMixerContext`]: xref:Bonsai.Mixer.CreateMixerContext
[`StartMixer`]: xref:Bonsai.Mixer.StartMixer
[`PlayBuffer`]: xref:Bonsai.Mixer.PlayBuffer
[`CreateSource`]: xref:Bonsai.Mixer.CreateSource
[`AppendBuffer`]: xref:Bonsai.Mixer.AppendBuffer
[`SetGain`]: xref:Bonsai.Mixer.SetGain
[`Mat`]: xref:OpenCV.Net.Mat

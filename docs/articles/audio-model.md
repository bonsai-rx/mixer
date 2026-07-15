---
uid: audio-model
---

# How the mixer works

The mixer is built around a real-time audio callback that PortAudio calls whenever the output device needs more samples. Understanding what that callback does, and what it deliberately does not do, explains the latency and why playback stays responsive.

## The audio callback

On each call, the callback fills the output with the sum of the active sources, each scaled by its gain. That is all it does: it runs no synthesis, loads no files, and allocates no buffers. The audio at the output is whatever was produced upstream and appended to the sources.

The callback does not clamp the summed result, so overlapping loud sources or a gain above 1 can push the output past the -1 to 1 range and distort.

## Latency

The mixer context reports the stream `OutputLatency`, the best estimate of the delay between a sample entering the mixer and leaving the device. A lower figure can be requested through the `SuggestedLatency` property when the context is created, but a request the device cannot meet may produce glitches, and some drivers ignore it and pick a latency they can sustain instead, so measuring on the target hardware is more reliable than assuming a value.

## Off the audio callback

Because the callback must return before the device runs short of samples, all the work of producing audio happens elsewhere. Buffers are generated or loaded with the `Bonsai.Dsp` operators in the workflow. The control operators follow the same discipline. Appending a buffer, setting the gain, starting, pausing, or stopping playback all queue a command that the source applies at the start of its next callback, rather than changing shared state directly from the workflow thread.

## Observing playback state

The same discipline applies in the other direction, to the state the mixer reports. Playback-state changes are collected on the audio thread but delivered from a separate background thread, so subscribing to [`PlaybackState`] to log or visualize what a source is doing never blocks the callback, and the audio and observation paths stay independent.

[`PlaybackState`]: xref:Bonsai.Mixer.PlaybackState

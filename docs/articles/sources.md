---
uid: sources
---

# Audio sources

A source is a controllable voice on the mixer. Where [`PlayBuffer`] plays a buffer once and forgets it, a source persists after it is created, so more audio can be appended to it, its gain changed, and its playback paused, resumed, or stopped while it plays. Every source mixes into the output stream alongside the others until it stops.

## Creating a source

[`CreateSource`] emits a source handle for each mixer context it receives. Connect it downstream of the mixer and store the handle in a `BehaviorSubject`, so it can be shared with the source operators, which queue their commands onto the source to be applied on the audio thread.

- The `Looping` property replays the playback queue continuously. Left unset, the source plays its queued buffers once.
- The `Playing` property starts the source immediately. Set it to false to create the source paused and start it later with [`PlaySource`], which is the arm-then-trigger pattern covered in [playback control](xref:playback-control).

## Appending buffers

[`AppendBuffer`] appends the audio buffers from its first sequence to the playback queue of every source in its second sequence. The buffers play in the order they arrive, picking up after whatever is already queued.

How the queue plays out then depends on whether the source loops:

- **A fixed looping sound.** Append one or more buffers to a looping source and it cycles through them continuously, so a recorded loop or a synthesized tone plays without a break.
- **A streaming source.** Append buffers to a non-looping source over time and it plays each one as it arrives, then goes idle when its queue empties. Buffers that have already played are reclaimed, so a long stream does not accumulate memory.

The workflow below shows the looping case, appending a generated tone to a looping source that plays without a break.

:::workflow
![Looping a source](~/workflows/sources-looping.bonsai)
:::

## Buffer format

An audio buffer is a [`Mat`] of 32-bit floating-point samples normalized to the range -1 to 1, where 1 is full-scale amplitude. Its columns run along time, and the row count decides how the buffer maps to the output channels:

- **A single row** is a mono buffer. The mixer broadcasts it across every output channel, scaled by the per-channel gain, so one row can be positioned anywhere across the speakers. See [Gain control](xref:gain-control) for more details.
- **One row per output channel** carries a separate signal for each speaker, matching the `ChannelCount` reported by the mixer context.

Any other row count is rejected, as is a buffer with no samples or a sample depth other than 32-bit floating point.

> [!TIP]
> Derive the sample rate from the mixer context rather than hardcoding it, so a generated buffer always matches the open stream.

[`CreateSource`]: xref:Bonsai.Mixer.CreateSource
[`AppendBuffer`]: xref:Bonsai.Mixer.AppendBuffer
[`PlayBuffer`]: xref:Bonsai.Mixer.PlayBuffer
[`PlaySource`]: xref:Bonsai.Mixer.PlaySource
[`Mat`]: xref:OpenCV.Net.Mat

---
uid: playback-control
---

# Playback control

Once a source handle is available, three operators control its playback, two manage its queue and one reports what state it is in. Each control operator queues its command onto the source, so the change takes effect on the next audio callback rather than instantly on the workflow thread.

## Playing, pausing, and stopping

- [`PlaySource`] resumes a paused source from its current position. Resuming a source that is already playing has no effect.
- [`PauseSource`] holds the source at its current position, keeping the queued buffers, and stops it contributing to the output until it resumes.
- [`StopSource`] fades the source to silence and then removes it from the mixer. The `FadeDuration` property, 20 milliseconds by default, keeps the stop smooth, since a hard cut can produce an audible click. A duration of zero stops playback immediately.

The difference between pausing and stopping is lifetime. A paused source stays on the mixer and can resume. Stopping removes the source permanently, so playing the same sound again requires creating a new source.

The workflow below plays a looping source, pauses it after two seconds, and resumes it two seconds later.

:::workflow
![Pausing and resuming a source](~/workflows/playback-control-pause-resume.bonsai)
:::

## Clearing and replacing queued audio

Two operators discard what a source has queued without removing the source itself.

- [`ClearBuffers`] discards every buffer queued on the source at the time the command is processed, including the one currently playing. The `FadeDuration` property, 20 milliseconds by default, fades the queued audio to silence first, since a cut mid-waveform can produce an audible click. A duration of zero discards the queue immediately.
- [`ReplaceBuffer`] does the same and queues a new buffer in the same step. No other buffer can be queued between the clear and the replacement, and the new buffer starts from its first sample on the sample after the fade ends.

Unlike [`StopSource`], both operators leave the source on the mixer with its gain, looping and playing settings intact. Buffers appended after the command are kept and start playing once the fade completes, or as soon as the queued audio runs out if that happens first. A paused source is cleared immediately, without fading. The playback state stays `Playing` during the fade, then reports `Idle` if nothing is left to play.

The old audio fades out, and only then does the new buffer start at full level. The two never overlap. This keeps the onset of the new sound exactly as it was built, which matters when a buffer carries its own envelope or a synchronisation edge.

The workflow below plays a looping tone, clears it after two seconds and appends a higher tone two seconds later. The gap between the fade and the new tone is silence, since nothing is queued.

:::workflow
![Clearing a source and appending a new buffer](~/workflows/playback-control-clear.bonsai)
:::

The next workflow replaces the looping tone with a higher one after two seconds. The new tone starts as soon as the fade ends.

:::workflow
![Replacing the queued buffer of a source](~/workflows/playback-control-replace.bonsai)
:::

[`ReplaceBuffer`] can also insert a gap of silence before the new buffer. The `GapDuration` property, zero by default, sets its length in seconds. The gap is counted in samples from the end of the fade, or from when the queued audio ran out if that came first. It plays once, even on a looping source. Pausing the source during the gap stops its countdown until playback resumes. The playback state stays `Playing` throughout, and a clear or replace during the gap discards the waiting buffer at once.

In the workflow above, try changing the `GapDuration` property of the [`ReplaceBuffer`] operator.

## Playback states

[`PlaybackState`] reports the state of each source, emitting its current value on subscription and on every subsequent transition, and completing when the source is removed. The state is a [`MixerSourceState`]:

- `Playing`: the source is moving through its queued buffers.
- `Paused`: the source is holding its position without contributing to the output.
- `Idle`: the source is still playing but out of samples. A non-looping source reaches this state once its queue empties, as does any source once its queue is cleared. It goes back to `Playing` if more buffers are appended.
- `Stopped`: the source has been removed from the mixer. This is the final state, and it completes the sequence.

State changes are reported off the audio thread, so observing or logging them does not affect audio timing. See [how the mixer works](xref:audio-model).

## Starting a source on a cue

Creating a source with `Playing` set to false, appending its buffer, and starting it with [`PlaySource`] on an external cue prepares the sound in advance. The cue-to-audio delay is then just the output latency rather than the cost of building the buffer. This is the arm-then-trigger pattern.

The workflow below creates a paused source, appends a tone to it straight away and starts it two seconds later, where the delay stands in for the cue.

:::workflow
![Arming a source and triggering it on a cue](~/workflows/playback-control-arm-trigger.bonsai)
:::

[`PlaySource`]: xref:Bonsai.Mixer.PlaySource
[`PauseSource`]: xref:Bonsai.Mixer.PauseSource
[`StopSource`]: xref:Bonsai.Mixer.StopSource
[`ClearBuffers`]: xref:Bonsai.Mixer.ClearBuffers
[`ReplaceBuffer`]: xref:Bonsai.Mixer.ReplaceBuffer
[`PlaybackState`]: xref:Bonsai.Mixer.PlaybackState
[`MixerSourceState`]: xref:Bonsai.Mixer.MixerSourceState

---
uid: playback-control
---

# Playback control

Once a source handle is available, three operators control its playback, and one reports what state it is in. Each control operator queues its command onto the source, so the change takes effect on the next audio callback rather than instantly on the workflow thread.

## Playing, pausing, and stopping

- [`PlaySource`] resumes a paused source from its current position. Resuming a source that is already playing has no effect.
- [`PauseSource`] holds the source at its current position, keeping the queued buffers, and stops it contributing to the output until it resumes.
- [`StopSource`] fades the source to silence and then removes it from the mixer. The `FadeDuration` property, 20 milliseconds by default, keeps the stop smooth, since a hard cut can produce an audible click. A duration of zero stops playback immediately.

The difference between pausing and stopping is lifetime. A paused source stays on the mixer and can resume. Stopping removes the source permanently, so playing the same sound again requires creating a new source.

The workflow below plays a looping source, pauses it after two seconds, and resumes it two seconds later.

:::workflow
![Pausing and resuming a source](~/workflows/playback-control-pause-resume.bonsai)
:::

## Playback states

[`PlaybackState`] reports the state of each source, emitting its current value on subscription and on every subsequent transition, and completing when the source is removed. The state is a [`MixerSourceState`]:

- `Playing`: the source is moving through its queued buffers.
- `Paused`: the source is holding its position without contributing to the output.
- `Idle`: the source is still playing but out of samples. A non-looping source reaches this state once its queue empties, and goes back to `Playing` if more buffers are appended.
- `Stopped`: the source has been removed from the mixer. This is the final state, and it completes the sequence.

State changes are reported off the audio thread, so observing or logging them does not affect audio timing. See [how the mixer works](xref:audio-model).

## Starting a source on a cue

Creating a source with `Playing` set to false, appending its buffer, and starting it with [`PlaySource`] on an external cue prepares the sound in advance. The cue-to-audio delay is then just the output latency rather than the cost of building the buffer. This is the arm-then-trigger pattern.

[`PlaySource`]: xref:Bonsai.Mixer.PlaySource
[`PauseSource`]: xref:Bonsai.Mixer.PauseSource
[`StopSource`]: xref:Bonsai.Mixer.StopSource
[`PlaybackState`]: xref:Bonsai.Mixer.PlaybackState
[`MixerSourceState`]: xref:Bonsai.Mixer.MixerSourceState

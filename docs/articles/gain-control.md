---
uid: gain-control
---

# Gain control

Every source has two independent gains that scale its samples on the way into the mixer. The source gain sets the overall level of the source, and the per-channel gain sets a separate level for each output channel. The gain applied to a channel is the product of the two, so the source gain controls how loud the source is while the per-channel gain controls where it appears to be across the speakers, and each can change without disturbing the other.

## Source gain

[`SetGain`] sets the source gain, which scales every output channel by the same amount. The gain is a linear multiplier on the samples rather than a level in decibels, so a `Gain` of 1 keeps the original amplitude, a value between 0 and 1 attenuates, and a value above 1 amplifies.

The change is not instantaneous. The `Duration` property sets how long the gain takes to reach the new level, 20 milliseconds by default, so it changes smoothly rather than jumping, which would produce an audible click. A duration of zero changes the gain immediately. If a new target is set before the previous one finishes, the gain continues from its current level instead of restarting.

A source can also start at a chosen level. [`CreateSource`] has a `Gain` property that sets the initial source gain, so a source can begin silent and fade in, or begin at a set level, avoiding a step from full amplitude when the first [`SetGain`] applies. In the workflow below, the source starts silent, then its gain rises steadily and drops sharply each cycle, and the 20 ms smoothing keeps even the sudden drop free of clicks.

:::workflow
![Modulating the gain](~/workflows/gain-modulation.bonsai)
:::

## Per-channel gain

[`SetChannelGain`] sets a separate level for each output channel from an array of values, one per channel. It applies on top of the source gain rather than replacing it, so changing the overall level with [`SetGain`] leaves the per-channel balance in place. On a source that already carries one signal per channel, this balances the channels against each other.

> [!WARNING]
> The length of the per-channel gain array must equal the mixer context `ChannelCount`.

On a single-row buffer, the signal is broadcast to every output channel, so the per-channel levels alone determine where the sound appears to be across the speakers. [`SetChannelGain`] has the same `Duration` property as [`SetGain`], so moving a source between the speakers is smooth rather than stepped.

For example, on a two-channel stream, a gain of `[1, 0]` places a mono source fully on the left, `[0, 1]` fully on the right, and equal levels put it in the center. Setting different levels on each channel this way is also called panning. The workflow below starts a mono source centered, then moves it fully left and then fully right.

:::workflow
![Positioning across channels](~/workflows/gain-per-channel.bonsai)
:::

[`SetGain`]: xref:Bonsai.Mixer.SetGain
[`SetChannelGain`]: xref:Bonsai.Mixer.SetChannelGain
[`CreateSource`]: xref:Bonsai.Mixer.CreateSource
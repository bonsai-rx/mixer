using System;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents a signal that ramps linearly toward a target value over a specified duration.
    /// </summary>
    /// <remarks>
    /// When a new target is set before the current ramp finishes, the value continues from where
    /// it is rather than restarting, keeping the output continuous.
    /// </remarks>
    internal sealed class LinearRamp : Signal
    {
        readonly double sampleRate;
        float current;
        float target;
        float increment;
        int remainingSamples;

        public LinearRamp(double sampleRate, float initialValue)
        {
            this.sampleRate = sampleRate;
            current = initialValue;
            target = initialValue;
        }

        public bool IsCompleted => remainingSamples == 0;

        public void SetTarget(float target, double duration)
        {
            this.target = target;
            remainingSamples = (int)Math.Max(0, duration * sampleRate);
            if (remainingSamples > 0)
            {
                increment = (target - current) / remainingSamples;
            }
            else
            {
                current = target;
                increment = 0f;
            }
        }

        /// <inheritdoc/>
        public override void Generate(Span<float> buffer)
        {
            var value = current;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (remainingSamples > 0)
                {
                    value += increment;
                    if (--remainingSamples == 0)
                        value = target;
                }

                buffer[i] = value;
            }

            current = value;
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            target = current;
            increment = 0f;
            remainingSamples = 0;
        }
    }
}

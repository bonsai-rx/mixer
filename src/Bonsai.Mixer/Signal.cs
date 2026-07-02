using System;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Represents a signal that generates successive blocks of samples on demand.
    /// </summary>
    /// <remarks>
    /// A signal fills a caller-provided buffer and never allocates, blocks, or throws while
    /// running on the audio thread. The mixer pulls a signal once per processing block.
    /// </remarks>
    public abstract class Signal
    {
        /// <summary>
        /// Generates the next block of samples into the specified buffer.
        /// </summary>
        /// <param name="buffer">
        /// The buffer to fill. The signal writes one value for each element and advances its
        /// internal state by that number of samples. The buffer is owned by the caller and must
        /// not be retained beyond the call.
        /// </param>
        public abstract void Generate(Span<float> buffer);

        /// <summary>
        /// Resets the signal to its initial state.
        /// </summary>
        public virtual void Reset()
        {
        }
    }
}

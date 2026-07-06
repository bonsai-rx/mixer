using System.Threading;

namespace Bonsai.Mixer
{
    /// <summary>
    /// A bounded single-producer, single-consumer ring buffer used to hand notifications off from
    /// the audio callback thread to the notification pump without locking or allocating.
    /// </summary>
    /// <remarks>
    /// The producer advances the tail and the consumer advances the head, each publishing its
    /// index after the element access so the other side observes a consistent slot. The capacity
    /// must be a power of two so the index wrap reduces to a bitmask.
    /// </remarks>
    /// <typeparam name="T">The type of the elements stored in the buffer.</typeparam>
    internal sealed class RingBuffer<T>
    {
        readonly T[] items;
        readonly int mask;
        int head;
        int tail;

        public RingBuffer(int capacity)
        {
            items = new T[capacity];
            mask = capacity - 1;
        }

        public bool TryEnqueue(T item)
        {
            var currentTail = tail;
            var next = (currentTail + 1) & mask;
            if (next == Volatile.Read(ref head))
                return false;

            items[currentTail] = item;
            Volatile.Write(ref tail, next);
            return true;
        }

        public bool TryDequeue(out T item)
        {
            var currentHead = head;
            if (currentHead == Volatile.Read(ref tail))
            {
                item = default;
                return false;
            }

            item = items[currentHead];
            items[currentHead] = default;
            Volatile.Write(ref head, (currentHead + 1) & mask);
            return true;
        }
    }
}

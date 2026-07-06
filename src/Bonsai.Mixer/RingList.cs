namespace Bonsai.Mixer
{
    /// <summary>
    /// A growable first-in first-out collection with indexed access, backed by a circular buffer.
    /// Single-threaded, unlike the lock-free <see cref="RingBuffer{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the elements stored in the collection.</typeparam>
    internal sealed class RingList<T>
    {
        T[] items = new T[4];
        int head;

        public int Count { get; private set; }

        public T this[int index] => items[(head + index) % items.Length];

        public void Add(T item)
        {
            if (Count == items.Length)
            {
                var newItems = new T[items.Length * 2];
                for (int i = 0; i < Count; i++)
                    newItems[i] = items[(head + i) % items.Length];
                items = newItems;
                head = 0;
            }

            items[(head + Count) % items.Length] = item;
            Count++;
        }

        public void RemoveRange(int count)
        {
            for (int i = 0; i < count; i++)
            {
                items[head] = default;
                head = (head + 1) % items.Length;
            }

            Count -= count;
        }
    }
}

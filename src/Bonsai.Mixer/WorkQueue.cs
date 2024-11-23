using System;
using System.Collections.Generic;

namespace Bonsai.Mixer
{
    /// <summary>
    /// Multiple producer / single consumer queue of work items.
    /// </summary>
    /// <typeparam name="T">The type of the elements stored in the work queue.</typeparam>
    /// <remarks>
    /// Provides support for multiple producers to asynchronously enqueue items to be
    /// processed by a single consumer. A double buffering strategy is used to ensure the
    /// consumer remains lock-free. Importantly, the buffer swap happens when the work on
    /// the consumer queue is entirely done.
    /// 
    /// It is possible producers are still in the process of adding an item during the
    /// swap, but there is no critical work done immediately after, so this is acceptable.
    /// Processing of all active work items is done on a different buffer altogether and
    /// the newly swapped out queue will only be accessed at the start of the next cycle.
    /// 
    /// The assumption is that by the time we are done with everything and need the next
    /// items we will be done with processing a single <c>Add</c> call.
    /// </remarks>
    internal class WorkQueue<T>
    {
        volatile List<T> producerQueue = new();
        volatile List<T> consumerQueue = new();
        readonly List<T> workItems = new();
        readonly object gate = new();

        public void Add(T item)
        {
            lock (gate)
            {
                producerQueue.Add(item);
            }
        }

        public void RemoveAll(Predicate<T> match)
        {
            var newItems = consumerQueue;
            workItems.AddRange(newItems);
            newItems.Clear();

            consumerQueue = producerQueue;
            producerQueue = newItems;
            workItems.RemoveAll(match);
        }

        public void Clear()
        {
            lock (gate)
            {
                workItems.Clear();
                producerQueue.Clear();
                consumerQueue.Clear();
            }
        }
    }
}

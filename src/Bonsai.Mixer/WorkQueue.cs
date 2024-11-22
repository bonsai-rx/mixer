using System;
using System.Collections.Generic;

namespace Bonsai.Mixer
{
    internal class WorkQueue<T>
    {
        List<T> frontQueue = new();
        List<T> backQueue = new();
        readonly List<T> workItems = new();
        readonly object gate = new();

        public void Add(T item)
        {
            lock (gate)
            {
                frontQueue.Add(item);
            }
        }

        public void RemoveAll(Predicate<T> match)
        {
            lock (gate)
            {
                (backQueue, frontQueue) = (frontQueue, backQueue);
            }

            workItems.AddRange(backQueue);
            workItems.RemoveAll(match);
            backQueue.Clear();
        }

        public void Clear()
        {
            lock (gate)
            {
                workItems.Clear();
                backQueue.Clear();
                frontQueue.Clear();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;

namespace LibraryTests.Nfs
{
    // https://blogs.msdn.microsoft.com/toub/2006/04/12/blocking-queues/
    class BlockingQueue<T>
    {
        private int _count = 0;
        private Queue<T> _queue = new Queue<T>();

        public T Dequeue()
        {
            lock (_queue)
            {
                while (_count <= 0)
                {
                    Monitor.Wait(_queue);
                }

                _count--;
                return _queue.Dequeue();
            }
        }

        public void Enqueue(T data)
        {
            if (data == null) throw new ArgumentNullException("data");

            lock (_queue)
            {
                _queue.Enqueue(data);
                _count++;
                Monitor.Pulse(_queue);
            }
        }
    }
}

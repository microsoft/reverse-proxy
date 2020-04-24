// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Utilities
{
    /// <summary>
    /// Alternative to SemaphoreSlim that respects the current thread scheduler.
    /// </summary>
    /// <remarks>
    /// Based on <c>https://devblogs.microsoft.com/pfxteam/building-async-coordination-primitives-part-5-asyncsemaphore/</c>.
    /// </remarks>
    public sealed class AsyncSemaphore
    {
        private readonly Queue<TaskCompletionSource<bool>> _waiters = new Queue<TaskCompletionSource<bool>>();
        private int _count;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncSemaphore"/> class.
        /// </summary>
        /// <param name="initialCount">The initial number of requests for the semaphore that can be granted concurrently.</param>
        public AsyncSemaphore(int initialCount)
        {
            Contracts.Check(initialCount >= 0, $"{nameof(initialCount)} must be non-negative");
            _count = initialCount;
        }

        /// <summary>
        /// Gets the number of available slots in this semaphore. In other words,
        /// how many times <see cref="WaitAsync"/> could be called without blocking.
        /// </summary>
        public int AvailableCount
        {
            get
            {
                lock (_waiters)
                {
                    return _count;
                }
            }
        }

        /// <summary>
        /// Asynchronously waits to enter the <see cref="AsyncSemaphore"/>.
        /// </summary>
        /// <returns>A task that tracks the asynchronous operation.</returns>
        public async Task WaitAsync()
        {
            Task task;
            lock (_waiters)
            {
                if (_count > 0)
                {
                    _count--;
                    return;
                }
                else
                {
                    var waiter = new TaskCompletionSource<bool>();
                    _waiters.Enqueue(waiter);
                    task = waiter.Task;
                }
            }

            await task;
        }

        /// <summary>
        /// Releases the <see cref="AsyncSemaphore"/> object once.
        /// </summary>
        public void Release()
        {
            TaskCompletionSource<bool> toRelease = null;
            lock (_waiters)
            {
                if (_waiters.Count > 0)
                {
                    toRelease = _waiters.Dequeue();
                }
                else
                {
                    _count++;
                }
            }

            toRelease?.SetResult(true);
        }
    }
}

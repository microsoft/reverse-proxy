// <copyright file="AsyncSemaphore.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading.Tasks;

namespace IslandGateway.CoreServicesBorrowed
{
    /// <summary>
    /// Alternative to SemaphoreSlim that respects the current thread scheduler.
    /// </summary>
    /// <remarks>
    /// Based on <c>https://blogs.msdn.microsoft.com/pfxteam/2012/02/12/building-async-coordination-primitives-part-5-asyncsemaphore/</c>.
    /// </remarks>
    public sealed class AsyncSemaphore
    {
        private readonly Queue<TaskCompletionSource<bool>> waiters = new Queue<TaskCompletionSource<bool>>();
        private int count;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncSemaphore"/> class.
        /// </summary>
        /// <param name="initialCount">The initial number of requests for the semaphore that can be granted concurrently.</param>
        public AsyncSemaphore(int initialCount)
        {
            Contracts.Check(initialCount >= 0, $"{nameof(initialCount)} must be non-negative");
            this.count = initialCount;
        }

        /// <summary>
        /// Gets the number of available slots in this semaphore. In other words,
        /// how many times <see cref="WaitAsync"/> could be called without blocking.
        /// </summary>
        public int AvailableCount
        {
            get
            {
                lock (this.waiters)
                {
                    return this.count;
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
            lock (this.waiters)
            {
                if (this.count > 0)
                {
                    this.count--;
                    return;
                }
                else
                {
                    var waiter = new TaskCompletionSource<bool>();
                    this.waiters.Enqueue(waiter);
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
            lock (this.waiters)
            {
                if (this.waiters.Count > 0)
                {
                    toRelease = this.waiters.Dequeue();
                }
                else
                {
                    this.count++;
                }
            }

            toRelease?.SetResult(true);
        }
    }
}

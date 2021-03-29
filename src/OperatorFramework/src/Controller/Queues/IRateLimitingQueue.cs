// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Kubernetes.Controller.Queues
{
    /// <summary>
    /// Interface IRateLimitingQueue
    /// Implements the <see cref="IDelayingQueue{TItem}" />.
    /// </summary>
    /// <typeparam name="TItem">The type of the t item.</typeparam>
    /// <seealso cref="IDelayingQueue{TItem}" />
    public interface IRateLimitingQueue<TItem> : IDelayingQueue<TItem>
    {
        /// <summary>
        /// AddRateLimited adds an item to the workqueue after the rate limiter says it's ok.
        /// </summary>
        /// <param name="item">The item.</param>
        void AddRateLimited(TItem item);

        /// <summary>
        /// Forget indicates that an item is finished being retried.  Doesn't matter whether it's for perm failing
        /// or for success, we'll stop the rate limiter from tracking it.  This only clears the `rateLimiter`, you
        /// still have to call `Done` on the queue.
        /// </summary>
        /// <param name="item">The item.</param>
        void Forget(TItem item);

        /// <summary>
        /// NumRequeues returns back how many times the item was requeued.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.Int32.</returns>
        int NumRequeues(TItem item);
    }
}

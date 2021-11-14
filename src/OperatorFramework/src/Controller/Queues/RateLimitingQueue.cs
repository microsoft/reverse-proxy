// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Controller.RateLimiters;

namespace Microsoft.Kubernetes.Controller.Queues;

/// <summary>
/// Class RateLimitingQueue is the default implemenation of <see cref="IRateLimitingQueue{TItem}" /> interface.
/// Implements the <see cref="Composition.DelayingQueueBase{TItem}" />.
/// Implements the <see cref="IRateLimitingQueue{TItem}" />.
/// </summary>
/// <typeparam name="TItem">The type of the t item.</typeparam>
/// <seealso cref="Composition.DelayingQueueBase{TItem}" />
/// <seealso cref="IRateLimitingQueue{TItem}" />
public class RateLimitingQueue<TItem> : Composition.DelayingQueueBase<TItem>, IRateLimitingQueue<TItem>
{
    private readonly IRateLimiter<TItem> _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitingQueue{TItem}" /> class.
    /// </summary>
    /// <param name="rateLimiter">The rate limiter.</param>
    public RateLimitingQueue(IRateLimiter<TItem> rateLimiter, IDelayingQueue<TItem> @base = default)
        : base(@base ?? new DelayingQueue<TItem>())
    {
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// AddRateLimited adds an item to the workqueue after the rate limiter says it's ok.
    /// </summary>
    /// <param name="item">The item.</param>
    public void AddRateLimited(TItem item)
    {
        AddAfter(item, _rateLimiter.ItemDelay(item));
    }

    /// <summary>
    /// Forget indicates that an item is finished being retried.  Doesn't matter whether it's for perm failing
    /// or for success, we'll stop the rate limiter from tracking it.  This only clears the `rateLimiter`, you
    /// still have to call `Done` on the queue.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Forget(TItem item)
    {
        _rateLimiter.Forget(item);
    }

    /// <summary>
    /// Numbers the requeues.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>System.Int32.</returns>
    public int NumRequeues(TItem item)
    {
        return _rateLimiter.NumRequeues(item);
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Controller.Rate;
using System;

namespace Microsoft.Kubernetes.Controller.RateLimiters;

/// <summary>
/// Class BucketRateLimiter adapts a standard bucket to the workqueue ratelimiter API.
/// https://github.com/kubernetes/client-go/blob/master/util/workqueue/default_rate_limiters.go#L48
/// Implements the <see cref="IRateLimiter{TItem}" />.
/// </summary>
/// <typeparam name="TItem">The type of the t item.</typeparam>
/// <seealso cref="IRateLimiter{TItem}" />
public class BucketRateLimiter<TItem> : IRateLimiter<TItem>
{
    private readonly Limiter _limiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="BucketRateLimiter{TItem}" /> class.
    /// </summary>
    /// <param name="limiter">The limiter.</param>
    public BucketRateLimiter(Limiter limiter)
    {
        _limiter = limiter;
    }

    /// <summary>
    /// When gets an item and gets to decide how long that item should wait.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>TimeSpan.</returns>
    public TimeSpan ItemDelay(TItem item)
    {
        return _limiter.Reserve().Delay();
    }

    /// <summary>
    /// NumRequeues returns back how many failures the item has had.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>System.Int32.</returns>
    public int NumRequeues(TItem item)
    {
        return 0;
    }

    /// <summary>
    /// Forget indicates that an item is finished being retried.  Doesn't matter whether its for perm failing
    /// or for success, we'll stop tracking it.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Forget(TItem item)
    {
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.Kubernetes.Controller.RateLimiters
{
    /// <summary>
    /// Interface IRateLimiter.
    /// https://github.com/kubernetes/client-go/blob/master/util/workqueue/default_rate_limiters.go#L27.
    /// </summary>
    /// <typeparam name="TItem">The type of the t item.</typeparam>
    public interface IRateLimiter<TItem>
    {
        /// <summary>
        /// When gets an item and gets to decide how long that item should wait.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>TimeSpan.</returns>
        TimeSpan ItemDelay(TItem item);

        /// <summary>
        /// Forget indicates that an item is finished being retried.  Doesn't matter whether its for perm failing
        /// or for success, we'll stop tracking it.
        /// </summary>
        /// <param name="item">The item.</param>
        void Forget(TItem item);

        /// <summary>
        /// NumRequeues returns back how many failures the item has had.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.Int32.</returns>
        int NumRequeues(TItem item);
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Kubernetes.Controller.RateLimiters
{
    /// <summary>
    /// Class MaxOfRateLimiter calls every RateLimiter and returns the worst case response
    /// When used with a token bucket limiter, the burst could be apparently exceeded in cases where particular items
    /// were separately delayed a longer time.
    /// https://github.com/kubernetes/client-go/blob/master/util/workqueue/default_rate_limiters.go#L175
    /// Implements the <see cref="IRateLimiter{TItem}" />.
    /// </summary>
    /// <typeparam name="TItem">The type of the t item.</typeparam>
    /// <seealso cref="IRateLimiter{TItem}" />
    public class MaxOfRateLimiter<TItem> : IRateLimiter<TItem>
    {
        private readonly IRateLimiter<TItem>[] _rateLimiters;

        /// <summary>
        /// Initializes a new instance of the <see cref="MaxOfRateLimiter{TItem}"/> class.
        /// </summary>
        /// <param name="rateLimiters">The rate limiters.</param>
        public MaxOfRateLimiter(params IRateLimiter<TItem>[] rateLimiters)
        {
            if (rateLimiters is null)
            {
                throw new ArgumentNullException(nameof(rateLimiters));
            }

            _rateLimiters = (IRateLimiter<TItem>[])rateLimiters.Clone();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MaxOfRateLimiter{TItem}"/> class.
        /// </summary>
        /// <param name="rateLimiters">The rate limiters.</param>
        public MaxOfRateLimiter(IEnumerable<IRateLimiter<TItem>> rateLimiters)
        {
            _rateLimiters = rateLimiters.ToArray();
        }

        /// <summary>
        /// When gets an item and gets to decide how long that item should wait.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>TimeSpan.</returns>
        public TimeSpan ItemDelay(TItem item)
        {
            // util\workqueue\default_rate_limiters.go:179
            var result = TimeSpan.Zero;
            foreach (var rateLimiter in _rateLimiters)
            {
                var current = rateLimiter.ItemDelay(item);
                if (result < current)
                {
                    result = current;
                }
            }

            return result;
        }

        /// <summary>
        /// NumRequeues returns back how many failures the item has had.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.Int32.</returns>
        public int NumRequeues(TItem item)
        {
            // util\workqueue\default_rate_limiters.go:185
            var result = 0;
            foreach (var rateLimiter in _rateLimiters)
            {
                result = Math.Max(result, rateLimiter.NumRequeues(item));
            }

            return result;
        }

        /// <summary>
        /// Forget indicates that an item is finished being retried.  Doesn't matter whether its for perm failing
        /// or for success, we'll stop tracking it.
        /// </summary>
        /// <param name="item">The item.</param>
        public void Forget(TItem item)
        {
            // util\workqueue\default_rate_limiters.go:207
            foreach (var rateLimiter in _rateLimiters)
            {
                rateLimiter.Forget(item);
            }
        }
    }
}

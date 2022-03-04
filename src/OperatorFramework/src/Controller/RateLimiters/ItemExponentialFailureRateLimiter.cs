// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Kubernetes.Controller.RateLimiters;

/// <summary>
/// Class ItemExponentialFailureRateLimiter does a simple baseDelay*2^{num-failures} limit
/// dealing with max failures and expiration are up to the caller.
/// https://github.com/kubernetes/client-go/blob/master/util/workqueue/default_rate_limiters.go#L67
/// Implements the <see cref="IRateLimiter{TItem}" />.
/// </summary>
/// <typeparam name="TItem">The type of the t item.</typeparam>
/// <seealso cref="IRateLimiter{TItem}" />
public class ItemExponentialFailureRateLimiter<TItem> : IRateLimiter<TItem>
{
    private readonly object _sync = new object();
    private readonly Dictionary<TItem, int> _itemFailures = new Dictionary<TItem, int>();
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemExponentialFailureRateLimiter{TItem}" /> class.
    /// </summary>
    /// <param name="baseDelay">The base delay.</param>
    /// <param name="maxDelay">The maximum delay.</param>
    public ItemExponentialFailureRateLimiter(TimeSpan baseDelay, TimeSpan maxDelay)
    {
        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
    }

    /// <inheritdoc/>
    public virtual TimeSpan ItemDelay(TItem item)
    {
        lock (_sync)
        {
            if (_itemFailures.TryGetValue(item, out var requeues))
            {
                _itemFailures[item] = requeues + 1;
            }
            else
            {
                _itemFailures.Add(item, 1);
            }

            var backoff = _baseDelay.TotalMilliseconds * Math.Pow(2, requeues);
            if (backoff > _maxDelay.TotalMilliseconds)
            {
                backoff = _maxDelay.TotalMilliseconds;
            }

            return TimeSpan.FromMilliseconds(backoff);
        }
    }

    /// <inheritdoc/>
    public virtual void Forget(TItem item)
    {
        lock (_sync)
        {
            _itemFailures.Remove(item);
        }
    }

    /// <inheritdoc/>
    public virtual int NumRequeues(TItem item)
    {
        lock (_sync)
        {
            return _itemFailures.TryGetValue(item, out var requeues) ? requeues : 0;
        }
    }
}

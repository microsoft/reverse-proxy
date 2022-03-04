// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Kubernetes.Controller.Queues.Composition;

/// <summary>
/// Class RateLimitingQueueBase is a delegating base class for <see cref="IRateLimitingQueue{TItem}" /> interface. These classes are
/// ported from go, which favors composition over inheritance, so the pattern is followed.
/// Implements the <see cref="DelayingQueueBase{TItem}" />.
/// Implements the <see cref="IRateLimitingQueue{TItem}" />.
/// </summary>
/// <typeparam name="TItem">The type of the t item.</typeparam>
/// <seealso cref="DelayingQueueBase{TItem}" />
/// <seealso cref="IRateLimitingQueue{TItem}" />
public abstract class RateLimitingQueueBase<TItem> : DelayingQueueBase<TItem>, IRateLimitingQueue<TItem>
{
    private readonly IRateLimitingQueue<TItem> _base;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitingQueueBase{TItem}" /> class.
    /// </summary>
    /// <param name="base">The base.</param>
    protected RateLimitingQueueBase(IRateLimitingQueue<TItem> @base)
        : base(@base)
    {
        _base = @base;
    }

    /// <inheritdoc/>
    public virtual void AddRateLimited(TItem item) => _base.AddRateLimited(item);

    /// <inheritdoc/>
    public virtual void Forget(TItem item) => _base.Forget(item);

    /// <inheritdoc/>
    public virtual int NumRequeues(TItem item) => _base.NumRequeues(item);
}

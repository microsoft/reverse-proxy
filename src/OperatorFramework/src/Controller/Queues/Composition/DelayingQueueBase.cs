// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.Kubernetes.Controller.Queues.Composition
{
    /// <summary>
    /// Class DelayingQueueBase is a delegating base class for <see cref="IDelayingQueue{TItem}" /> interface. These classes are
    /// ported from go, which favors composition over inheritance, so the pattern is followed.
    /// Implements the <see cref="WorkQueueBase{TItem}" />.
    /// Implements the <see cref="IDelayingQueue{TItem}" />.
    /// </summary>
    /// <typeparam name="TItem">The type of the t item.</typeparam>
    /// <seealso cref="WorkQueueBase{TItem}" />
    /// <seealso cref="IDelayingQueue{TItem}" />
    public abstract class DelayingQueueBase<TItem> : WorkQueueBase<TItem>, IDelayingQueue<TItem>
    {
        private readonly IDelayingQueue<TItem> _base;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelayingQueueBase{TItem}" /> class.
        /// </summary>
        /// <param name="base">The base.</param>
        protected DelayingQueueBase(IDelayingQueue<TItem> @base)
            : base(@base)
        {
            _base = @base;
        }

        /// <inheritdoc/>
        public void AddAfter(TItem item, TimeSpan delay) => _base.AddAfter(item, delay);
    }
}

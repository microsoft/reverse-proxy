// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.Kubernetes.Controller.Queues
{
    /// <summary>
    /// Interface IDelayingQueue
    /// Implements the <see cref="IWorkQueue{TItem}" />.
    /// </summary>
    /// <typeparam name="TItem">The type of the t item.</typeparam>
    /// <seealso cref="IWorkQueue{TItem}" />
    public interface IDelayingQueue<TItem> : IWorkQueue<TItem>
    {
        /// <summary>
        /// Adds the after.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="delay">The delay.</param>
        void AddAfter(TItem item, TimeSpan delay);
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;

namespace Microsoft.Kubernetes.Operator.Caches
{
    /// <summary>
    /// Class OperatorCache.
    /// Implements the <see cref="IOperatorCache{TResource}" />.
    /// </summary>
    /// <typeparam name="TResource">The type of the t resource.</typeparam>
    /// <seealso cref="IOperatorCache{TResource}" />
    public class OperatorCache<TResource> : IOperatorCache<TResource>
        where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
    {
        private readonly Dictionary<NamespacedName, OperatorCacheWorkItem<TResource>> _workItems = new Dictionary<NamespacedName, OperatorCacheWorkItem<TResource>>();
        private readonly object _workItemsSync = new object();

        /// <inheritdoc/>
        public bool TryGetWorkItem(NamespacedName namespacedName, out OperatorCacheWorkItem<TResource> workItem)
        {
            lock (_workItemsSync)
            {
                return _workItems.TryGetValue(namespacedName, out workItem);
            }
        }

        /// <inheritdoc/>
        public void UpdateWorkItem(NamespacedName namespacedName, UpdateWorkItem<TResource> update)
        {
            if (update is null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            lock (_workItemsSync)
            {
                if (_workItems.TryGetValue(namespacedName, out var workItem))
                {
                    // alter an existing entry
                    workItem = update(workItem);
                    if (workItem.IsEmpty)
                    {
                        // remove if result has no information
                        _workItems.Remove(namespacedName);
                    }
                    else
                    {
                        // otherwise update struct in dictionary
                        _workItems[namespacedName] = workItem;
                    }
                }
                else
                {
                    workItem = update(OperatorCacheWorkItem<TResource>.Empty);
                    if (workItem.IsEmpty == false)
                    {
                        // add if result has information
                        _workItems.Add(namespacedName, workItem);
                    }
                }
            }
        }
    }
}

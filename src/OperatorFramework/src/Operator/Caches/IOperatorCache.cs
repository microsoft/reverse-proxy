// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;

namespace Microsoft.Kubernetes.Operator.Caches
{
    /// <summary>
    /// Updates an empty or existing work item.
    /// </summary>
    /// <typeparam name="TResource">The type of the operator resource.</typeparam>
    /// <param name="workItem">An existing or default work item.</param>
    /// <returns></returns>
    public delegate OperatorCacheWorkItem<TResource> UpdateWorkItem<TResource>(OperatorCacheWorkItem<TResource> workItem)
        where TResource : class, IKubernetesObject<V1ObjectMeta>, new();

    /// <summary>
    /// Interface IOperatorCache.
    /// </summary>
    /// <typeparam name="TResource">The type of the t resource.</typeparam>
    public interface IOperatorCache<TResource>
        where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
    {
        /// <summary>
        /// Updates the work item.
        /// </summary>
        /// <param name="namespacedName">Name of the namespaced.</param>
        /// <param name="update">The update.</param>
        void UpdateWorkItem(NamespacedName namespacedName, UpdateWorkItem<TResource> update);

        /// <summary>
        /// Tries the get work item.
        /// </summary>
        /// <param name="namespacedName">Name of the namespaced.</param>
        /// <param name="workItem">The work item.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool TryGetWorkItem(NamespacedName namespacedName, out OperatorCacheWorkItem<TResource> workItem);
    }
}

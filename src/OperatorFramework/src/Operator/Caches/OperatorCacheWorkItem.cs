// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Kubernetes.Operator.Caches
{
    /// <summary>
    /// Class OperatorCacheWorkItem contains the information needed to reconcile a resource's desired
    /// state with what's known to exist in the cluster.
    /// </summary>
    /// <typeparam name="TResource">The type of the operator resource.</typeparam>
    public struct OperatorCacheWorkItem<TResource> : IEquatable<OperatorCacheWorkItem<TResource>> where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
    {
        public static readonly OperatorCacheWorkItem<TResource> Empty = new OperatorCacheWorkItem<TResource>(
            resource: default,
            related: ImmutableDictionary<GroupKindNamespacedName, IKubernetesObject<V1ObjectMeta>>.Empty);

        public OperatorCacheWorkItem(
            TResource resource,
            ImmutableDictionary<GroupKindNamespacedName, IKubernetesObject<V1ObjectMeta>> related)
        {
            Resource = resource;
            Related = related;
        }

        /// <summary>
        /// Gets the operator resource.
        /// </summary>
        /// <value>The operator resource.</value>
        public TResource Resource { get; }

        /// <summary>
        /// Gets the related resource which are owned by the operator resource.
        /// </summary>
        /// <value>The related resources.</value>
        public ImmutableDictionary<GroupKindNamespacedName, IKubernetesObject<V1ObjectMeta>> Related { get; }

        public bool IsEmpty => Resource == null && Related.IsEmpty;

        /// <summary>
        /// Returns a WorkItem with Resource assigned to new value.
        /// </summary>
        /// <param name="resource">The new operator resource.</param>
        /// <returns></returns>
        public OperatorCacheWorkItem<TResource> SetResource(TResource resource)
        {
            return new OperatorCacheWorkItem<TResource>(resource, Related);
        }

        /// <summary>
        /// Returns a WorkItem with Related assigned to new value.
        /// </summary>
        /// <param name="resource">The new related resources.</param>
        /// <returns></returns>
        public OperatorCacheWorkItem<TResource> SetRelated(ImmutableDictionary<GroupKindNamespacedName, IKubernetesObject<V1ObjectMeta>> related)
        {
            return new OperatorCacheWorkItem<TResource>(Resource, related);
        }

        public override bool Equals(object obj)
        {
            return obj is OperatorCacheWorkItem<TResource> item && Equals(item);
        }

        public bool Equals(OperatorCacheWorkItem<TResource> other)
        {
            return EqualityComparer<TResource>.Default.Equals(Resource, other.Resource) &&
                   EqualityComparer<ImmutableDictionary<GroupKindNamespacedName, IKubernetesObject<V1ObjectMeta>>>.Default.Equals(Related, other.Related);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Resource, Related);
        }

        public static bool operator ==(OperatorCacheWorkItem<TResource> left, OperatorCacheWorkItem<TResource> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(OperatorCacheWorkItem<TResource> left, OperatorCacheWorkItem<TResource> right)
        {
            return !(left == right);
        }
    }
}

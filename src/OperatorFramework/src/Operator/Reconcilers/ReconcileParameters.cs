// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;

namespace Microsoft.Kubernetes.Operator;

public class ReconcileParameters<TResource>
    where TResource : class, IKubernetesObject<V1ObjectMeta>
{
    public ReconcileParameters(TResource resource, IDictionary<GroupKindNamespacedName, IKubernetesObject<V1ObjectMeta>> relatedResources)
    {
        Resource = resource;
        RelatedResources = relatedResources ?? throw new ArgumentNullException(nameof(relatedResources));
    }

    public TResource Resource { get; }
    public IDictionary<GroupKindNamespacedName, IKubernetesObject<V1ObjectMeta>> RelatedResources { get; }
}

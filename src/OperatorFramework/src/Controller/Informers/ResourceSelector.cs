// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;

namespace Microsoft.Kubernetes.Controller.Informers;

/// <summary>
/// Provides a mechanism for <see cref="ResourceInformer{TResource}"/> to constrain search based on fields in the resource.
/// </summary>
public class ResourceSelector<TResource>
    where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
{
    public ResourceSelector(string fieldSelector)
    {
        FieldSelector = fieldSelector;
    }

    public string FieldSelector { get; } = null;
}

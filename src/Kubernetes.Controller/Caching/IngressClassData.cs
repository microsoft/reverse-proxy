// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using k8s.Models;

namespace Yarp.Kubernetes.Controller.Caching;

/// <summary>
/// Holds data needed from a <see cref="V1IngressClass"/> resource.
/// </summary>
public struct IngressClassData
{
    public IngressClassData(V1IngressClass ingressClass)
    {
        if (ingressClass is null)
        {
            throw new ArgumentNullException(nameof(ingressClass));
        }

        IngressClass = ingressClass;
        IsDefault = GetDefaultAnnotation(ingressClass);
    }

    public V1IngressClass IngressClass { get; }

    public bool IsDefault { get; }

    private static bool GetDefaultAnnotation(V1IngressClass ingressClass)
    {
        var annotation = ingressClass.GetAnnotation("ingressclass.kubernetes.io/is-default-class");
        return string.Equals("true", annotation, StringComparison.OrdinalIgnoreCase);
    }
}

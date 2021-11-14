// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using System;
using System.Collections.Generic;

namespace Yarp.Kubernetes.Controller.Caching;

/// <summary>
/// Holds data needed from a <see cref="V1Endpoints"/> resource.
/// </summary>
#pragma warning disable CA1815 // Override equals and operator equals on value types
public struct Endpoints
#pragma warning restore CA1815 // Override equals and operator equals on value types
{
    public Endpoints(V1Endpoints endpoints)
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        Name = endpoints.Name();
        Subsets = endpoints.Subsets;
    }

    public string Name { get; set; }
    public IList<V1EndpointSubset> Subsets { get; }
}

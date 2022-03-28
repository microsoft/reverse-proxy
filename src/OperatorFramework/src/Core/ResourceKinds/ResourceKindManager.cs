// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.ResourceKinds;

public class ResourceKindManager : IResourceKindManager
{
    private readonly IEnumerable<IResourceKindProvider> _providers;

    public ResourceKindManager(IEnumerable<IResourceKindProvider> providers)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    public async Task<IResourceKind> GetResourceKindAsync(string apiVersion, string kind)
    {
        foreach (var provider in _providers)
        {
            var resourceKind = await provider.GetResourceKindAsync(apiVersion, kind);
            if (resourceKind is not null)
            {
                return resourceKind;
            }
        }
        return DefaultResourceKind.Unknown;
    }
}

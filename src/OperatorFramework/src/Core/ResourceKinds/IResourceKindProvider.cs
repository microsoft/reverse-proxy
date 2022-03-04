// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Microsoft.Kubernetes.ResourceKinds;

public interface IResourceKindProvider
{
    public Task<IResourceKind> GetResourceKindAsync(string apiVersion, string kind);
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Management
{
    /// <summary>
    /// Manages the runtime state of clusters.
    /// </summary>
    internal interface IClusterManager : IItemManager<ClusterInfo>
    {
    }
}

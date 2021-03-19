// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.Management
{
    /// <summary>
    /// Manages the runtime state of clusters.
    /// </summary>
    internal interface IClusterManager : IItemManager<ClusterInfo>
    {
    }
}

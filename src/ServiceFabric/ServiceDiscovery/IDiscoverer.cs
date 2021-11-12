// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.ServiceFabric;

/// <summary>
/// Discovers Service Fabric services and builds the corresponding
/// <see cref="RouteConfig"/> and <see cref="ClusterConfig"/> instances that represent them.
/// </summary>
internal interface IDiscoverer
{
    /// <summary>
    /// Execute the discovery and update entities.
    /// </summary>
    Task<(IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters)> DiscoverAsync(CancellationToken cancellation);
}

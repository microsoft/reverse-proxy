// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Abstractions;

namespace Yarp.ReverseProxy.Service
{
    /// <summary>
    /// A configuration filter that will run each time the proxy configuration is loaded.
    /// </summary>
    public interface IProxyConfigFilter
    {
        /// <summary>
        /// Allows modification of a cluster configuration.
        /// </summary>
        /// <param name="cluster">The <see cref="ClusterConfig"/> instance to configure.</param>
        ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel);

        /// <summary>
        /// Allows modification of a route configuration.
        /// </summary>
        /// <param name="route">The <see cref="RouteConfig"/> instance to configure.</param>
        ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, CancellationToken cancel);
    }
}

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
        /// <param name="id">The id for the cluster.</param>
        /// <param name="cluster">The <see cref="Cluster"/> instance to configure.</param>
        ValueTask<Cluster> ConfigureClusterAsync(Cluster cluster, CancellationToken cancel);

        /// <summary>
        /// Allows modification of a route configuration.
        /// </summary>
        /// <param name="route">The <see cref="RouteConfig"/> instance to configure.</param>
        ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, CancellationToken cancel);
    }
}

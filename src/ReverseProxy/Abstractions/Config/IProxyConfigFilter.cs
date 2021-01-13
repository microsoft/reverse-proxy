// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.Service
{
    /// <summary>
    /// A configuration filter that will run each time the proxy configuration is loaded.
    /// </summary>
    public interface IProxyConfigFilter
    {
        /// <summary>
        /// Allows modification of a Cluster configuration.
        /// </summary>
        /// <param name="id">The id for the cluster.</param>
        /// <param name="cluster">The Cluster instance to configure.</param>
        Task ConfigureClusterAsync(Cluster cluster, CancellationToken cancel);

        /// <summary>
        /// Allows modification of a route configuration.
        /// </summary>
        /// <param name="route">The ProxyRoute instance to configure.</param>
        Task<ProxyRoute> ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel);
    }
}

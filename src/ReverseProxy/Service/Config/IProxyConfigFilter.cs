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
        /// Allows modification of a Backend configuration.
        /// </summary>
        /// <param name="id">The id for the backend.</param>
        /// <param name="backend">The Backend instance to configure.</param>
        Task ConfigureBackendAsync(Backend backend, CancellationToken cancel);

        /// <summary>
        /// Allows modification of a route configuration.
        /// </summary>
        /// <param name="route">The ProxyRoute instance to configure.</param>
        Task ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel);
    }
}

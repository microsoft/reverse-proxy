// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// Discovers Service Fabric services that want to use the Island Gateway and builds the needed abstractions for it.
    /// </summary>
    /// <remarks>Implementations should take the Island Gateway's repos in the constructor.</remarks>
    internal interface IDiscoverer
    {
        /// <summary>
        /// Execute the discovery and update entities.
        /// </summary>
        Task<(IReadOnlyList<ProxyRoute> Routes, IReadOnlyList<Cluster> Clusters)> DiscoverAsync(CancellationToken cancellation);
    }
}

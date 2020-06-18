// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Manages the set of clusters. Changes only become effective when
    /// <see cref="IReverseProxyConfigManager.ApplyConfigurationsAsync"/> is called.
    /// </summary>
    public interface IClustersRepo
    {
        /// <summary>
        /// Gets the current set of clusters.
        /// </summary>
        Task<IDictionary<string, Cluster>> GetClustersAsync(CancellationToken cancellation);

        /// <summary>
        /// Sets the current set of clusters.
        /// </summary>
        Task SetClustersAsync(IDictionary<string, Cluster> clusters, CancellationToken cancellation);
    }
}

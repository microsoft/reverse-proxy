// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Actively monitors destinations health.
    /// </summary>
    public interface IActiveHealthCheckMonitor
    {
        /// <summary>
        /// Checks health of all clusters' destinations.
        /// </summary>
        /// <param name="clusters">Clusters to check the health of their destinations.</param>
        /// <returns><see cref="Task"/> representing the health check process.</returns>
        Task CheckHealthAsync(IEnumerable<ClusterInfo> clusters);
    }
}

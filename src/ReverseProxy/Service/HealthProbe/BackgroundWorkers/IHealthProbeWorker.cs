// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.ReverseProxy.Service.Management;

namespace Microsoft.ReverseProxy.Service.HealthProbe
{
    /// <summary>
    /// Interface for the active health probe background worker,
    /// which verifies health of cluster endpoints by attempting communication
    /// with them.
    /// </summary>
    internal interface IHealthProbeWorker
    {
        /// <summary>
        /// Starts issuing active health probes for all the clusters configured in
        /// <see cref="IClusterManager"/>.
        /// </summary>
        Task UpdateTrackedClusters();

        /// <summary>
        /// Gracefully terminates all active health probing activity.
        /// </summary>
        Task StopAsync();
    }
}

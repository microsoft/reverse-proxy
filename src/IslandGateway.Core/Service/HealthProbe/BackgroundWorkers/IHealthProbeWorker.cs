// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using IslandGateway.Core.Service.Management;

namespace IslandGateway.Core.Service.HealthProbe
{
    /// <summary>
    /// Interface for the active health probe background worker,
    /// which verifies health of backend endpoints by attempting communication
    /// with them.
    /// </summary>
    internal interface IHealthProbeWorker
    {
        /// <summary>
        /// Starts issuing active health probes for all the backends configured in
        /// <see cref="IBackendManager"/>.
        /// </summary>
        Task UpdateTrackedBackends();

        /// <summary>
        /// Gracefully terminates all active health probing activity.
        /// </summary>
        Task StopAsync();
    }
}

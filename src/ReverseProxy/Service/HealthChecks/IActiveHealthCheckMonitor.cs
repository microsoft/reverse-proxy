// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

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
        /// <returns><see cref="Task"/> representing the health check process.</returns>
        Task CheckHealthAsync();
    }
}

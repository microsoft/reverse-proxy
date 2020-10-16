// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Actively monitors destinations health.
    /// </summary>
    public interface IActiveHealthCheckMonitor : IDisposable
    {
        /// <summary>
        /// Force health check of all clusters' destinations.
        /// </summary>
        /// <returns><see cref="Task"/> representing the health check process.</returns>
        Task ForceCheckAll();
    }
}

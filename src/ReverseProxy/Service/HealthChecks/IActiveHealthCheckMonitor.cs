// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

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
        void ForceCheckAll();
    }
}

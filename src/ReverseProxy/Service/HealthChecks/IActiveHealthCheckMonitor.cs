// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Actively monitors destinations health.
    /// </summary>
    public interface IActiveHealthCheckMonitor : IDisposable
    {
        /// <summary>
        /// Force health checks of all given clusters' destinations.
        /// </summary>
        /// <param name="allClusters">Clusters whose destinations' health will be checked.</param>
        /// <returns><see cref="Task"/> representing an asyncronous health check operation.</returns>
        Task ForceCheckAll(IEnumerable<ClusterInfo> allClusters);
    }
}
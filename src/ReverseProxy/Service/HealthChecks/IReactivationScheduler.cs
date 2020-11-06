// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;
using System;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Reactivates a destination by restoring it's passive health state to <see cref="DestinationHealth.Unknown"/> after some period.
    /// </summary>
    public interface IReactivationScheduler
    {
        /// <summary>
        /// Schedules restoring a destination as <see cref="DestinationHealth.Unknown"/>.
        /// </summary>
        /// <param name="cluster">Cluster.</param>
        /// <param name="destination">Destination marked as <see cref="DestinationHealth.Unhealthy"/> by the passive health check.</param>
        /// <param name="reactivationPeriod">Reactivation period.</param>
        void Schedule(ClusterInfo cluster, DestinationInfo destination, TimeSpan reactivationPeriod);
    }
}

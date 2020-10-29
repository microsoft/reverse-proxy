// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Updates destinations' health states when it's requested by a health check policy
    /// while taking into account not only the new evaluated value but also the overall current cluster's health state.
    /// </summary>
    public interface IDestinationHealthUpdater
    {
        /// <summary>
        /// Asynchronously sets the passive health on the given <paramref name="destination"/>.
        /// </summary>
        /// <param name="cluster">Cluster.</param>
        /// <param name="destination">Destination.</param>
        /// <param name="newHealth">New passive health value.</param>
        /// <param name="reactivationPeriod">If <paramref name="newHealth"/> is <see cref="DestinationHealth.Unhealthy"/>,
        /// this parameter secifies a reactivation period after which the destination's <see cref="CompositeDestinationHealth.Passive"/> value
        /// will be reset to <see cref="DestinationHealth.Unknown"/>. Otherwise, it's not used.</param>
        /// <returns><see cref="Task"/> representing a passive health update operation.</returns>
        Task SetPassiveAsync(ClusterInfo cluster, DestinationInfo destination, DestinationHealth newHealth, TimeSpan reactivationPeriod);

        /// <summary>
        /// Asynchronously sets the active health values on the given destinations.
        /// </summary>
        /// <param name="cluster">Cluster.</param>
        /// <param name="newHealths">New active health values.</param>
        /// <returns><see cref="Task"/> representing an active health update operation.</returns>
        Task SetActiveAsync(ClusterInfo cluster, IEnumerable<(DestinationInfo Destination, DestinationHealth NewHealth)> newHealths);
    }
}

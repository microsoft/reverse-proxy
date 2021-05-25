// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Updates the cluster's destination collections.
    /// </summary>
    public interface IClusterDestinationsUpdater
    {
        /// <summary>
        /// Updates the cluster's collection of destination available for proxying requests to.
        /// Call this if health state has changed for any destinations.
        /// </summary>
        /// <param name="cluster">The <see cref="ClusterState"/> owing the destinations.</param>
        public void UpdateAvailableDestinations(ClusterState cluster);

        /// <summary>
        /// Updates the cluster's collection of all configured destinations.
        /// Call this after destinations have been added, removed, or their configuration changed.
        /// This does not need to be called for state updates like health, use UpdateAvailableDestinations for state updates.
        /// </summary>
        /// <param name="cluster">The <see cref="ClusterState"/> owing the destinations.</param>
        public void UpdateAllDestinations(ClusterState cluster);
    }
}

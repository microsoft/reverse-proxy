// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Representation of a cluster for use at runtime.
    /// </summary>
    /// <remarks>
    /// Note that while this class is immutable, specific members such as
    /// <see cref="Config"/> and <see cref="DynamicState"/> hold mutable references
    /// that can be updated atomically and which will always have latest information
    /// relevant to this cluster.
    /// All members are thread safe.
    /// </remarks>
    public sealed class ClusterInfo
    {
        private volatile ClusterDynamicState _dynamicState = new ClusterDynamicState(Array.Empty<DestinationInfo>(), Array.Empty<DestinationInfo>());
        private volatile ClusterConfig _config;

        internal ClusterInfo(string clusterId, IDestinationManager destinationManager)
        {
            ClusterId = clusterId ?? throw new ArgumentNullException(nameof(clusterId));
            DestinationManager = destinationManager ?? throw new ArgumentNullException(nameof(destinationManager));
        }

        public string ClusterId { get; }

        /// <summary>
        /// Encapsulates parts of a cluster that can change atomically
        /// in reaction to config changes.
        /// </summary>
        public ClusterConfig Config
        {
            get => _config;
            internal set => _config = value ?? throw new ArgumentNullException(nameof(value));
        }

        internal IDestinationManager DestinationManager { get; }

        /// <summary>
        /// Encapsulates parts of a cluster that can change atomically
        /// in reaction to runtime state changes (e.g. dynamic endpoint discovery).
        /// </summary>
        public ClusterDynamicState DynamicState => _dynamicState;

        /// <summary>
        /// Keeps track of the total number of concurrent requests on this cluster.
        /// </summary>
        internal AtomicCounter ConcurrencyCounter { get; } = new AtomicCounter();

        /// <summary>
        /// Recreates the DynamicState data.
        /// </summary>
        public void UpdateDynamicState()
        {
            var healthChecks = _config?.HealthCheckOptions ?? default;
            var allDestinations = DestinationManager.Items;
            var healthyDestinations = allDestinations;

            if (healthChecks.Enabled)
            {
                var activeEnabled = healthChecks.Active.Enabled;
                var passiveEnabled = healthChecks.Passive.Enabled;

                healthyDestinations = allDestinations.Where(destination =>
                {
                    // Only consider the current state if those checks are enabled.
                    var state = destination.DynamicState;
                    var active = activeEnabled ? state.Health.Active : DestinationHealth.Unknown;
                    var passive = passiveEnabled ? state.Health.Passive : DestinationHealth.Unknown;

                    // Filter out unhealthy ones. Unknown state is OK, all destinations start that way.
                    return passive != DestinationHealth.Unhealthy && active != DestinationHealth.Unhealthy;
                }).ToList().AsReadOnly();
            }

            _dynamicState = new ClusterDynamicState(allDestinations, healthyDestinations);
        }
    }
}

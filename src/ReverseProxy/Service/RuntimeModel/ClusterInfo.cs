// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
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
        private readonly object _stateLock = new object();
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
            UpdateDynamicStateInternal(force: false);
        }

        internal void ForceUpdateDynamicState()
        {
            UpdateDynamicStateInternal(force: true);
        }

        private void UpdateDynamicStateInternal(bool force)
        {
            // Prevent overlapping updates. If there are multiple signals that state needs to be updated,
            // we want to ensure that updates don't conflict with each other. E.g. if state changes
            // while an update is already in progress, the next update should wait until the current one finishes
            // to ensure they don't race to set _dynamicState and end up with the stale one overwriting the fresh one.
            var lockTaken = false;
            try
            {
                if (force)
                {
                    Monitor.Enter(_stateLock, ref lockTaken);
                }
                else
                {
                    Monitor.TryEnter(_stateLock, ref lockTaken);
                }

                if (!lockTaken)
                {
                    return;
                }

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
                            var healthState = destination.Health;
                        var active = activeEnabled ? healthState.Active : DestinationHealth.Unknown;
                        var passive = passiveEnabled ? healthState.Passive : DestinationHealth.Unknown;

                            // Filter out unhealthy ones. Unknown state is OK, all destinations start that way.
                            return passive != DestinationHealth.Unhealthy && active != DestinationHealth.Unhealthy;
                    }).ToList().AsReadOnly();
                }

                _dynamicState = new ClusterDynamicState(allDestinations, healthyDestinations);
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_stateLock);
                }
            }
        }
    }
}

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
        private readonly SemaphoreSlim _updateRequests = new SemaphoreSlim(2);

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
            // Prevent overlapping updates and debounce extra concurrent calls.
            // If there are multiple concurrent calls to rebuild the dynamic state, we want to ensure that
            // updates don't conflict with each other. Additionally, we debounce extra concurrent calls if
            // they arrive in a quick succession to avoid spending too much CPU on frequent state rebuilds.
            // Specifically, only up to two threads are allowed to wait here and actually execute a rebuild,
            // all others will be debounced and the call will return without updating the _dynamicState.
            // However, changes made by those debounced threads (e.g. destination health updates) will be
            // taken into account by one of blocked threads after they get unblocked to run a rebuild.
            var lockTaken = false;
            if (force)
            {
                lockTaken = true;
                _updateRequests.Wait();
            }
            else
            {
                lockTaken = _updateRequests.Wait(0);
            }

            if (!lockTaken)
            {
                return;
            }

            lock (_stateLock)
            {
                try
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
                    // Semaphore is released while still holding the lock to AVOID the following case.
                    // The first thread (T1) finished a rebuild and left the lock while still holding the semaphore. The second thread (T2)
                    // waiting on the lock gets awaken, proceeds under the lock and begins the next rebuild. If at this exact moment
                    // the third thread (T3) enters this method and tries to acquire the semaphore, it will be debounced because
                    // the semaphore's count is still 0. However, T2 could have already made some progress and didnt' observe updates made
                    // by T3.
                    // By releasing the semaphore under the lock, we make sure that in the above situation T3 will proceed till the lock and
                    // its updates will be observed anyways.
                    _updateRequests.Release();
                }
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Signals;
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
        private readonly DelayableSignal<Unit> _destinationsStateSignal;

        internal ClusterInfo(string clusterId, IDestinationManager destinationManager)
        {
            ClusterId = clusterId ?? throw new ArgumentNullException(nameof(clusterId));
            DestinationManager = destinationManager ?? throw new ArgumentNullException(nameof(destinationManager));

            _destinationsStateSignal = CreateDestinationsStateSignal();
            DynamicStateSignal = CreateDynamicStateQuery();
        }

        public string ClusterId { get; }

        public ClusterConfig Config => ConfigSignal.Value;

        internal IDestinationManager DestinationManager { get; }

        /// <summary>
        /// Encapsulates parts of a cluster that can change atomically
        /// in reaction to config changes.
        /// </summary>
        internal Signal<ClusterConfig> ConfigSignal { get; } = SignalFactory.Default.CreateSignal<ClusterConfig>();

        /// <summary>
        /// Encapsulates parts of a cluster that can change atomically
        /// in reaction to runtime state changes (e.g. dynamic endpoint discovery).
        /// </summary>
        internal IReadableSignal<ClusterDynamicState> DynamicStateSignal { get; }

        /// <summary>
        /// A snapshot of the current dynamic state.
        /// </summary>
        public ClusterDynamicState DynamicState => DynamicStateSignal.Value;

        public void PauseHealthyDestinationUpdates()
        {
            _destinationsStateSignal.Pause();
        }

        public void ResumeHealthyDestinationUpdates()
        {
            _destinationsStateSignal.Resume();
        }

        /// <summary>
        /// Keeps track of the total number of concurrent requests on this cluster.
        /// </summary>
        internal AtomicCounter ConcurrencyCounter { get; } = new AtomicCounter();

        private DelayableSignal<Unit> CreateDestinationsStateSignal()
        {
            return DestinationManager.Items
                .SelectMany(destinations =>destinations.Select(destination => destination.DynamicStateSignal).AnyChange())
                .DropValue()
                .ToDelayable();
        }

        /// <summary>
        /// Sets up the data flow that keeps <see cref="DynamicState"/> up to date.
        /// See <c>Signals\Readme.md</c> for more information.
        /// </summary>
        private IReadableSignal<ClusterDynamicState> CreateDynamicStateQuery()
        {
            return new[] { _destinationsStateSignal, ConfigSignal.DropValue() }
                .AnyChange() // If any of them change...
                .Select(
                    _ =>
                    {
                        var allDestinations = DestinationManager.Items.Value ?? new List<DestinationInfo>().AsReadOnly();
                        var healthyDestinations = (Config?.HealthCheckOptions.Enabled ?? false)
                            ? allDestinations.Where(destination => destination.DynamicState?.Health.Current != DestinationHealth.Unhealthy).ToList().AsReadOnly()
                            : allDestinations;
                        return new ClusterDynamicState(
                            allDestinations: allDestinations,
                            healthyDestinations: healthyDestinations);
                    });
        }
    }
}

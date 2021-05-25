// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Representation of a cluster for use at runtime.
    /// </summary>
    public sealed class ClusterState
    {
        private volatile ClusterDynamicState _dynamicState = new ClusterDynamicState(Array.Empty<DestinationState>(), Array.Empty<DestinationState>());
        private volatile ClusterModel _model = default!; // Initialized right after construction.

        /// <summary>
        /// Creates a new instance. This constructor is for tests and infrastructure, this type is normally constructed by the configuration
        /// loading infrastructure.
        /// </summary>
        public ClusterState(string clusterId)
        {
            ClusterId = clusterId ?? throw new ArgumentNullException(nameof(clusterId));
        }

        /// <summary>
        /// The cluster's unique id.
        /// </summary>
        public string ClusterId { get; }

        /// <summary>
        /// Encapsulates parts of a cluster that can change atomically in reaction to config changes.
        /// </summary>
        public ClusterModel Model
        {
            get => _model;
            internal set => _model = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// All of the destinations associated with this cluster. This collection is populated by the configuration system
        /// and should only be directly modified in a test environment.
        /// Call <see cref="IClusterDestinationsUpdater"/> after modifying this collection.
        /// </summary>
        public ConcurrentDictionary<string, DestinationState> Destinations { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Encapsulates parts of a cluster that can change atomically
        /// in reaction to runtime state changes (e.g. dynamic endpoint discovery).
        /// </summary>
        public ClusterDynamicState DynamicState
        {
            get => _dynamicState;
            set => _dynamicState = value;
        }

        /// <summary>
        /// Keeps track of the total number of concurrent requests on this cluster.
        /// </summary>
        internal AtomicCounter ConcurrencyCounter { get; } = new AtomicCounter();

        /// <summary>
        /// Tracks changes to the cluster configuration for use with rebuilding dependent endpoints.
        /// </summary>
        internal int Revision { get; set; }
    }
}

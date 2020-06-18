// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of a cluster
    /// that only change in reaction to configuration changes
    /// (e.g. health check options).
    /// </summary>
    /// <remarks>
    /// All members must remain immutable to avoid thread safety issues.
    /// Instead, instances of <see cref="ClusterConfig"/> are replaced
    /// in ther entirety when values need to change.
    /// </remarks>
    public sealed class ClusterConfig
    {
        public ClusterConfig(
            ClusterHealthCheckOptions healthCheckOptions,
            ClusterLoadBalancingOptions loadBalancingOptions,
            ClusterSessionAffinityOptions sessionAffinityOptions)
        {
            HealthCheckOptions = healthCheckOptions;
            LoadBalancingOptions = loadBalancingOptions;
            SessionAffinityOptions = sessionAffinityOptions;
        }

        public ClusterHealthCheckOptions HealthCheckOptions { get; }

        public ClusterLoadBalancingOptions LoadBalancingOptions { get; }

        public ClusterSessionAffinityOptions SessionAffinityOptions { get; }

        /// <summary>
        /// Active health probing options for a cluster.
        /// </summary>
        /// <remarks>
        /// Struct used only to keep things organized as we add more configuration options inside of `ClusterConfig`.
        /// Each "feature" can have its own struct.
        /// </remarks>
        public readonly struct ClusterHealthCheckOptions
        {
            public ClusterHealthCheckOptions(bool enabled, TimeSpan interval, TimeSpan timeout, int port, string path)
            {
                Enabled = enabled;
                Interval = interval;
                Timeout = timeout;
                Port = port;
                Path = path;
            }

            /// <summary>
            /// Whether health probes are enabled.
            /// </summary>
            public bool Enabled { get; }

            /// <summary>
            /// Interval between health probes.
            /// </summary>
            public TimeSpan Interval { get; }

            /// <summary>
            /// Health probe timeout, after which the targeted endpoint is considered unhealthy.
            /// </summary>
            public TimeSpan Timeout { get; }

            /// <summary>
            /// Port number.
            /// </summary>
            public int Port { get; }

            /// <summary>
            /// Http path.
            /// </summary>
            public string Path { get; }
        }

        public readonly struct ClusterLoadBalancingOptions
        {
            public ClusterLoadBalancingOptions(LoadBalancingMode mode)
            {
                Mode = mode;
                // Increment returns the new value and we want the first return value to be 0.
                RoundRobinState = new AtomicCounter() { Value = -1 };
            }

            public LoadBalancingMode Mode { get; }

            internal AtomicCounter RoundRobinState { get; }
        }

        public readonly struct ClusterSessionAffinityOptions
        {
            public ClusterSessionAffinityOptions(bool enabled, string mode, string failurePolicy, IReadOnlyDictionary<string, string> settings)
            {
                Mode = mode;
                FailurePolicy = failurePolicy;
                Settings = settings;
                Enabled = enabled;
            }

            public bool Enabled { get; }

            public string Mode { get; }

            public string FailurePolicy { get; }

            public IReadOnlyDictionary<string, string> Settings { get;  }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.ReverseProxy.Abstractions;

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
        private readonly Cluster _cluster;

        public ClusterConfig(
            Cluster cluster,
            ClusterHealthCheckOptions healthCheckOptions,
            ClusterLoadBalancingOptions loadBalancingOptions,
            ClusterSessionAffinityOptions sessionAffinityOptions,
            HttpMessageInvoker httpClient,
            ClusterProxyHttpClientOptions httpClientOptions,
            IReadOnlyDictionary<string, string> metadata)
        {
            _cluster = cluster;
            HealthCheckOptions = healthCheckOptions;
            LoadBalancingOptions = loadBalancingOptions;
            SessionAffinityOptions = sessionAffinityOptions;
            HttpClient = httpClient;
            HttpClientOptions = httpClientOptions;
            Metadata = metadata;
        }

        public ClusterHealthCheckOptions HealthCheckOptions { get; }

        public ClusterLoadBalancingOptions LoadBalancingOptions { get; }

        public ClusterSessionAffinityOptions SessionAffinityOptions { get; }

        public ClusterProxyHttpClientOptions HttpClientOptions { get; }

        /// <summary>
        /// An <see cref="HttpMessageInvoker"/> that used for proxying requests to an upstream server.
        /// </summary>
        public HttpMessageInvoker HttpClient { get; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this cluster.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }

        internal bool HasConfigChanged(ClusterConfig newClusterConfig)
        {
            return !Cluster.Equals(_cluster, newClusterConfig._cluster);
        }

        /// <summary>
        /// All health check options for a cluster.
        /// </summary>
        /// <remarks>
        /// Struct used only to keep things organized as we add more configuration options inside of `ClusterConfig`.
        /// Each "feature" can have its own struct.
        /// </remarks>
        public readonly struct ClusterHealthCheckOptions
        {
            public ClusterHealthCheckOptions(ClusterPassiveHealthCheckOptions passive, ClusterActiveHealthCheckOptions active)
            {
                Passive = passive;
                Active = active;
            }

            /// <summary>
            /// Whether at least one type of health check is enabled.
            /// </summary>
            public bool Enabled => Passive.Enabled || Active.Enabled;

            /// <summary>
            /// Passive health check options.
            /// </summary>
            public ClusterPassiveHealthCheckOptions Passive { get; }

            /// <summary>
            /// Active health check options.
            /// </summary>
            public ClusterActiveHealthCheckOptions Active { get; }
        }

        /// <summary>
        /// Passive health check options for a cluster.
        /// </summary>
        public readonly struct ClusterPassiveHealthCheckOptions
        {
            public ClusterPassiveHealthCheckOptions(bool enabled, string policy, TimeSpan? reactivationPeriod)
            {
                Enabled = enabled;
                Policy = policy;
                ReactivationPeriod = reactivationPeriod;
            }

            /// <summary>
            /// Whether active health checks are enabled.
            /// </summary>
            public bool Enabled { get; }

            /// <summary>
            /// Passive health check policy.
            /// </summary>
            public string Policy { get; }

            /// <summary>
            /// Destination reactivation period after which an unhealthy destination is considered healthy again.
            /// </summary>
            public TimeSpan? ReactivationPeriod { get; }
        }

        /// <summary>
        /// Active health check options for a cluster.
        /// </summary>
        public readonly struct ClusterActiveHealthCheckOptions
        {
            public ClusterActiveHealthCheckOptions(bool enabled, TimeSpan? interval, TimeSpan? timeout, string policy, string path)
            {
                Enabled = enabled;
                Interval = interval;
                Timeout = timeout;
                Policy = policy;
                Path = path;
            }

            /// <summary>
            /// Whether active health checks are enabled.
            /// </summary>
            public bool Enabled { get; }

            /// <summary>
            /// Health probe interval.
            /// </summary>
            // TODO: Consider switching to ISO8601 duration (e.g. "PT5M")
            public TimeSpan? Interval { get; }

            /// <summary>
            /// Health probe timeout, after which a destination is considered unhealthy.
            /// </summary>
            public TimeSpan? Timeout { get; }

            /// <summary>
            /// Active health check policy.
            /// </summary>
            public string Policy { get; }

            /// <summary>
            /// HTTP health check endpoint path.
            /// </summary>
            public string Path { get; }
        }
    }
}

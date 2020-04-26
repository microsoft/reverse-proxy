// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Core.Util;
using ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies;

namespace Microsoft.ReverseProxy.Core.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of a backend
    /// that only change in reaction to configuration changes
    /// (e.g. health check options).
    /// </summary>
    /// <remarks>
    /// All members must remain immutable to avoid thread safety issues.
    /// Instead, instances of <see cref="BackendConfig"/> are replaced
    /// in ther entirety when values need to change.
    /// </remarks>
    public sealed class BackendConfig
    {
        public BackendConfig(
            BackendHealthCheckOptions healthCheckOptions,
            BackendLoadBalancingOptions loadBalancingOptions)
        {
            HealthCheckOptions = healthCheckOptions;
            LoadBalancingOptions = loadBalancingOptions;
        }

        public BackendHealthCheckOptions HealthCheckOptions { get; }

        public BackendLoadBalancingOptions LoadBalancingOptions { get; }

        /// <summary>
        /// Active health probing options for a backend.
        /// </summary>
        /// <remarks>
        /// Struct used only to keep things organized as we add more configuration options inside of `BackendConfig`.
        /// Each "feature" can have its own struct.
        /// </remarks>
        public readonly struct BackendHealthCheckOptions
        {
            public BackendHealthCheckOptions(bool enabled, TimeSpan interval, TimeSpan timeout, int port, string path)
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

        public readonly struct BackendLoadBalancingOptions
        {
            public BackendLoadBalancingOptions(LoadBalancingMode mode,
                Func<IEnumerable<EndpointInfo>, BackendLoadBalancingOptions, EndpointInfo> callback = null,
                IDictionary<EndpointInfo, int> deficitRoundRobinQuanta = null,
                Func<EndpointInfo> failOverPreferredEndpoint = null,
                Predicate<EndpointInfo> failOverIsAvailablePredicate = null,
                Func<ILoadBalancingStrategy> failOverFallBackLoadBalancingStrategy = null,
                Func<IEnumerable<EndpointInfo>, IEnumerable<EndpointInfo>> trafficAllocationSelector = null,
                decimal? trafficAllocationVariation = null,
                Func<ILoadBalancingStrategy> trafficAllocationBackingLoadBalancingStrategy = null
                )
            {
                Mode = mode;

                // Increment returns the new value and we want the first return value to be 0.
                RoundRobinState = new AtomicCounter() { Value = -1 };

                Callback = callback;

                DeficitRoundRobinQuanta = deficitRoundRobinQuanta;

                FailOverPreferredEndpoint = failOverPreferredEndpoint;
                FailOverIsAvailablePredicate = failOverIsAvailablePredicate;
                FailOverFallBackLoadBalancingStrategy = failOverFallBackLoadBalancingStrategy;

                TrafficAllocationSelector = trafficAllocationSelector;
                TrafficAllocationVariation = trafficAllocationVariation;
                TrafficAllocationBackingLoadBalancingStrategy = trafficAllocationBackingLoadBalancingStrategy;
            }

            public LoadBalancingMode Mode { get; }

            public AtomicCounter RoundRobinState { get; }

            public Func<IEnumerable<EndpointInfo>, BackendLoadBalancingOptions, EndpointInfo> Callback { get; }

            public IDictionary<EndpointInfo, int> DeficitRoundRobinQuanta { get; }

            public Func<EndpointInfo> FailOverPreferredEndpoint { get; }
            public Predicate<EndpointInfo> FailOverIsAvailablePredicate { get; }
            public Func<ILoadBalancingStrategy> FailOverFallBackLoadBalancingStrategy { get; }

            public Func<IEnumerable<EndpointInfo>, IEnumerable<EndpointInfo>> TrafficAllocationSelector { get; }
            public decimal? TrafficAllocationVariation { get; }
            public Func<ILoadBalancingStrategy> TrafficAllocationBackingLoadBalancingStrategy { get; }
        }
    }
}

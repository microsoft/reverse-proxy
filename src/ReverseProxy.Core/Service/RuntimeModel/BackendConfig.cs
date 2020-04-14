// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

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
    internal sealed class BackendConfig
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
        internal readonly struct BackendHealthCheckOptions
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

        internal readonly struct BackendLoadBalancingOptions
        {
            public BackendLoadBalancingOptions(LoadBalancingMode mode)
            {
                Mode = mode;
            }

            public enum LoadBalancingMode
            {
                First,
                Random,
                PowerOfTwoChoices,
            }

            public LoadBalancingMode Mode { get; }
        }
    }
}

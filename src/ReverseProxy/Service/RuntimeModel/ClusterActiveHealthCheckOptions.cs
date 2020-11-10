// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.RuntimeModel
{
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

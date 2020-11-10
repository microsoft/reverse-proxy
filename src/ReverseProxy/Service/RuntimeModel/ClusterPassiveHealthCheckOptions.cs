// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.RuntimeModel
{
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
}

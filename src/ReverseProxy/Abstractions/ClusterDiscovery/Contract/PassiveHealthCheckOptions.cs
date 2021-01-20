// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Passive health check options.
    /// </summary>
    public sealed record PassiveHealthCheckOptions : IEquatable<PassiveHealthCheckOptions>
    {
        /// <summary>
        /// Whether passive health checks are enabled.
        /// </summary>
        public bool? Enabled { get; init; }

        /// <summary>
        /// Passive health check policy.
        /// </summary>
        public string Policy { get; init; }

        /// <summary>
        /// Destination reactivation period after which an unhealthy destination is considered healthy again.
        /// </summary>
        public TimeSpan? ReactivationPeriod { get; init; }

        public bool Equals(PassiveHealthCheckOptions other)
        {
            if (other == null)
            {
                return false;
            }

            return Enabled == other.Enabled
                && string.Equals(Policy, other.Policy, StringComparison.OrdinalIgnoreCase)
                && ReactivationPeriod == other.ReactivationPeriod;
        }
    }
}

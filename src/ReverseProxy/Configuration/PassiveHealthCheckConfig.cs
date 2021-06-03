// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Configuration
{
    /// <summary>
    /// Passive health check config.
    /// </summary>
    public sealed record PassiveHealthCheckConfig
    {
        /// <summary>
        /// Whether passive health checks are enabled.
        /// </summary>
        public bool? Enabled { get; init; }

        /// <summary>
        /// Passive health check policy.
        /// </summary>
        public string? Policy { get; init; }

        /// <summary>
        /// Destination reactivation period after which an unhealthy destination is considered healthy again.
        /// </summary>
        public TimeSpan? ReactivationPeriod { get; init; }

        /// <inheritdoc />
        public bool Equals(PassiveHealthCheckConfig? other)
        {
            if (other == null)
            {
                return false;
            }

            return Enabled == other.Enabled
                && string.Equals(Policy, other.Policy, StringComparison.OrdinalIgnoreCase)
                && ReactivationPeriod == other.ReactivationPeriod;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Enabled,
                Policy?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                ReactivationPeriod);
        }
    }
}

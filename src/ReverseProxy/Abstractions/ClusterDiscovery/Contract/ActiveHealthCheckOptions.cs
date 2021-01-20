// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Active health check options.
    /// </summary>
    public sealed record ActiveHealthCheckOptions : IEquatable<ActiveHealthCheckOptions>
    {
        /// <summary>
        /// Whether active health checks are enabled.
        /// </summary>
        public bool? Enabled { get; init; }

        /// <summary>
        /// Health probe interval.
        /// </summary>
        public TimeSpan? Interval { get; init; }

        /// <summary>
        /// Health probe timeout, after which a destination is considered unhealthy.
        /// </summary>
        public TimeSpan? Timeout { get; init; }

        /// <summary>
        /// Active health check policy.
        /// </summary>
        public string Policy { get; init; }

        /// <summary>
        /// HTTP health check endpoint path.
        /// </summary>
        public string Path { get; init; }

        public bool Equals(ActiveHealthCheckOptions other)
        {
            if (other == null)
            {
                return false;
            }

            return Enabled == other.Enabled
                && Interval == other.Interval
                && Timeout == other.Timeout
                && string.Equals(Policy, other.Policy, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);
        }
    }
}

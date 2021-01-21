// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// All health check options.
    /// </summary>
    public sealed record HealthCheckOptions
    {
        /// <summary>
        /// Passive health check options.
        /// </summary>
        public PassiveHealthCheckOptions Passive { get; init; }

        /// <summary>
        /// Active health check options.
        /// </summary>
        public ActiveHealthCheckOptions Active { get; init; }

        public bool Enabled => (Passive?.Enabled).GetValueOrDefault()
            || (Active?.Enabled).GetValueOrDefault();

        /// <inheritdoc />
        public bool Equals(HealthCheckOptions other)
        {
            if (other == null)
            {
                return false;
            }

            return Passive == other.Passive
                && Active == other.Active;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Passive, Active);
        }
    }
}

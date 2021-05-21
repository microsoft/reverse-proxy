// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Abstractions
{
    /// <summary>
    /// All health check config.
    /// </summary>
    public sealed record HealthCheckConfig
    {
        /// <summary>
        /// Passive health check config.
        /// </summary>
        public PassiveHealthCheckConfig? Passive { get; init; }

        /// <summary>
        /// Active health check config.
        /// </summary>
        public ActiveHealthCheckConfig? Active { get; init; }

        /// <inheritdoc />
        public bool Equals(HealthCheckConfig? other)
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

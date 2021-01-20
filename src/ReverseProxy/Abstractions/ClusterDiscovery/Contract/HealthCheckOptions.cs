// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// All health check options.
    /// </summary>
    public sealed record HealthCheckOptions : IEquatable<HealthCheckOptions>
    {
        /// <summary>
        /// Passive health check options.
        /// </summary>
        public PassiveHealthCheckOptions Passive { get; init; }

        /// <summary>
        /// Active health check options.
        /// </summary>
        public ActiveHealthCheckOptions Active { get; init; }

        public bool Equals(HealthCheckOptions other)
        {
            if (other == null)
            {
                return false;
            }

            return Passive == other.Passive && Active == other.Active;
        }
    }
}

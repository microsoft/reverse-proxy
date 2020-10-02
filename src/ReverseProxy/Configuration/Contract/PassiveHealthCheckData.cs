// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    /// <summary>
    /// Passive health check options.
    /// </summary>
    public sealed class PassiveHealthCheckData
    {
        /// <summary>
        /// Whether active health checks are enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Passive health check policy.
        /// </summary>
        public string Policy { get; set; }

        /// <summary>
        /// Destination reactivation period after which an unhealthy destination is considered healthy again.
        /// </summary>
        public TimeSpan ReactivationPeriod { get; set; }
    }
}

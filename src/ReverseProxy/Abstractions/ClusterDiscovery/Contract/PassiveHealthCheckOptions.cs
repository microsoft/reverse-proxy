// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Passive health check options.
    /// </summary>
    public sealed class PassiveHealthCheckOptions
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

        internal PassiveHealthCheckOptions DeepClone()
        {
            return new PassiveHealthCheckOptions
            {
                Enabled = Enabled,
                Policy = Policy,
                ReactivationPeriod = ReactivationPeriod,
            };
        }

        internal static bool Equals(PassiveHealthCheckOptions options1, PassiveHealthCheckOptions options2)
        {
            if (options1 == null && options2 == null)
            {
                return true;
            }

            if (options1 == null || options2 == null)
            {
                return false;
            }

            return options1.Enabled == options2.Enabled
                && string.Equals(options1.Policy, options2.Policy, StringComparison.OrdinalIgnoreCase)
                && options1.ReactivationPeriod == options2.ReactivationPeriod;
        }
    }
}

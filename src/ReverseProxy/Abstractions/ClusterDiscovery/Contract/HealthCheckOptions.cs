// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// All health check options.
    /// </summary>
    public sealed class HealthCheckOptions
    {
        /// <summary>
        /// Passive health check options.
        /// </summary>
        public PassiveHealthCheckOptions Passive { get; set; }

        /// <summary>
        /// Active health check options.
        /// </summary>
        public ActiveHealthCheckOptions Active { get; set; }

        internal HealthCheckOptions DeepClone()
        {
            return new HealthCheckOptions
            {
                Passive = Passive?.DeepClone(),
                Active = Active?.DeepClone()
            };
        }

        internal static bool Equals(HealthCheckOptions options1, HealthCheckOptions options2)
        {
            if (options1 == null && options2 == null)
            {
                return true;
            }

            if (options1 == null || options2 == null)
            {
                return false;
            }

            return PassiveHealthCheckOptions.Equals(options1.Passive, options2.Passive)
                && ActiveHealthCheckOptions.Equals(options1.Active, options2.Active);
        }
    }
}

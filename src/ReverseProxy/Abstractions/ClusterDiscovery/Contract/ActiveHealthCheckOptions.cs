// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Active health check options.
    /// </summary>
    public sealed class ActiveHealthCheckOptions
    {
        /// <summary>
        /// Whether active health checks are enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Health probe interval.
        /// </summary>
        public TimeSpan? Interval { get; set; }

        /// <summary>
        /// Health probe timeout, after which a destination is considered unhealthy.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Active health check policy.
        /// </summary>
        public string Policy { get; set; }

        /// <summary>
        /// HTTP health check endpoint path.
        /// </summary>
        public string Path { get; set; }

        internal ActiveHealthCheckOptions DeepClone()
        {
            return new ActiveHealthCheckOptions
            {
                Enabled = Enabled,
                Interval = Interval,
                Timeout = Timeout,
                Policy = Policy,
                Path = Path,
            };
        }

        internal static bool Equals(ActiveHealthCheckOptions options1, ActiveHealthCheckOptions options2)
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
                && options1.Interval == options2.Interval
                && options1.Timeout == options2.Timeout
                && string.Equals(options1.Policy, options2.Policy, StringComparison.OrdinalIgnoreCase)
                && string.Equals(options1.Path, options2.Path, StringComparison.OrdinalIgnoreCase);
        }
    }
}

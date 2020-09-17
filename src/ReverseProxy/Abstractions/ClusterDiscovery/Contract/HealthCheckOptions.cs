// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Active health check options.
    /// </summary>
    public sealed class HealthCheckOptions
    {
        /// <summary>
        /// Whether health probes are enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Health probe interval.
        /// </summary>
        // TODO: Consider switching to ISO8601 duration (e.g. "PT5M")
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// Health probe timeout, after which the targeted endpoint is considered unhealthy.
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Port number.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Http path.
        /// </summary>
        public string Path { get; set; }

        internal HealthCheckOptions DeepClone()
        {
            return new HealthCheckOptions
            {
                Enabled = Enabled,
                Interval = Interval,
                Timeout = Timeout,
                Port = Port,
                Path = Path,
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

            return options1.Enabled == options2.Enabled
                && options1.Interval == options2.Interval
                && options1.Timeout == options2.Timeout
                && options1.Port == options2.Port
                && string.Equals(options1.Path, options2.Path, StringComparison.OrdinalIgnoreCase);
        }
    }
}

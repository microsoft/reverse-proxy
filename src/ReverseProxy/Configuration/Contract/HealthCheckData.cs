// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    /// <summary>
    /// Active health check options.
    /// </summary>
    public sealed class HealthCheckData
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
    }
}

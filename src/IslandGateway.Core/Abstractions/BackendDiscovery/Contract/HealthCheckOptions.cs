// <copyright file="HealthCheckOptions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;

namespace IslandGateway.Core.Abstractions
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
                Enabled = this.Enabled,
                Interval = this.Interval,
                Timeout = this.Timeout,
                Port = this.Port,
                Path = this.Path,
            };
        }
    }
}

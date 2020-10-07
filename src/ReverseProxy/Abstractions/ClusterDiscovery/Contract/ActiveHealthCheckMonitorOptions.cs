// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Defines options for the active health check monitor.
    /// </summary>
    public class ActiveHealthCheckMonitorOptions
    {
        /// <summary>
        /// Default probing interval.
        /// </summary>
        public TimeSpan DefaultInterval { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Default probes timeout.
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(10);
    }
}

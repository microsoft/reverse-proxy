// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Active health probing options for a cluster.
    /// </summary>
    /// <remarks>
    /// Struct used only to keep things organized as we add more configuration options inside of `ClusterConfig`.
    /// Each "feature" can have its own struct.
    /// </remarks>
    public readonly struct ClusterHealthCheckOptions
    {
        public ClusterHealthCheckOptions(bool enabled, TimeSpan interval, TimeSpan timeout, int port, string path)
        {
            Enabled = enabled;
            Interval = interval;
            Timeout = timeout;
            Port = port;
            Path = path;
        }

        /// <summary>
        /// Whether health probes are enabled.
        /// </summary>
        public bool Enabled { get; }

        /// <summary>
        /// Interval between health probes.
        /// </summary>
        public TimeSpan Interval { get; }

        /// <summary>
        /// Health probe timeout, after which the targeted endpoint is considered unhealthy.
        /// </summary>
        public TimeSpan Timeout { get; }

        /// <summary>
        /// Port number.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Http path.
        /// </summary>
        public string Path { get; }
    }
}

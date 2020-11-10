// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// All health check options for a cluster.
    /// </summary>
    /// <remarks>
    /// Struct used only to keep things organized as we add more configuration options inside of `ClusterConfig`.
    /// Each "feature" can have its own struct.
    /// </remarks>
    public readonly struct ClusterHealthCheckOptions
    {
        public ClusterHealthCheckOptions(ClusterPassiveHealthCheckOptions passive, ClusterActiveHealthCheckOptions active)
        {
            Passive = passive;
            Active = active;
        }

        /// <summary>
        /// Whether at least one type of health check is enabled.
        /// </summary>
        public bool Enabled => Passive.Enabled || Active.Enabled;

        /// <summary>
        /// Passive health check options.
        /// </summary>
        public ClusterPassiveHealthCheckOptions Passive { get; }

        /// <summary>
        /// Active health check options.
        /// </summary>
        public ClusterActiveHealthCheckOptions Active { get; }
    }
}

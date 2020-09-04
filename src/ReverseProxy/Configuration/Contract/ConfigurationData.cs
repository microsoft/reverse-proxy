// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    /// <summary>
    /// Root of <see cref="Extensions.Configuration.IConfiguration"/>-first configuration model.
    /// </summary>
    public class ConfigurationData
    {
        /// <summary>
        /// Clusters.
        /// </summary>
        public Dictionary<string, ClusterData> Clusters { get; } = new Dictionary<string, ClusterData>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Routes.
        /// </summary>
        public List<ProxyRouteData> Routes { get; } = new List<ProxyRouteData>();
    }
}

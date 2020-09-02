// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    internal class ConfigurationData
    {
        public Dictionary<string, ClusterData> Clusters { get; } = new Dictionary<string, ClusterData>(StringComparer.OrdinalIgnoreCase);
        public List<ProxyRouteData> Routes { get; } = new List<ProxyRouteData>();
    }
}

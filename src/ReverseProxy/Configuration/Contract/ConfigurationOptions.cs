// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    internal class ConfigurationOptions
    {
        public Dictionary<string, Cluster> Clusters { get; } = new Dictionary<string, Cluster>(StringComparer.OrdinalIgnoreCase);
        public List<ProxyRoute> Routes { get; } = new List<ProxyRoute>();
    }
}

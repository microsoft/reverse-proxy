// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.Configuration
{
    internal class ProxyConfigOptions
    {
        public IDictionary<string, Cluster> Clusters { get; } = new Dictionary<string, Cluster>(StringComparer.Ordinal);
        public IList<ProxyRoute> Routes { get; } = new List<ProxyRoute>();
    }
}

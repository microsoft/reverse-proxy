// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.ConfigModel
{
    internal class DynamicConfigRoot
    {
        public IDictionary<string, Cluster> Clusters { get; set; }
        public IList<ProxyRoute> Routes { get; set; }
    }
}

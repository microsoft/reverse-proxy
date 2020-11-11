// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Service;

namespace Microsoft.ReverseProxy.Configuration
{
    internal class ConfigurationSnapshot : IProxyConfig
    {
        public List<ProxyRoute> Routes { get; internal set; } = new List<ProxyRoute>();

        public List<Cluster> Clusters { get; internal set; } = new List<Cluster>();

        IReadOnlyList<ProxyRoute> IProxyConfig.Routes => Routes;

        IReadOnlyList<Cluster> IProxyConfig.Clusters => Clusters;

        public IChangeToken ChangeToken { get; internal set; }
    }
}

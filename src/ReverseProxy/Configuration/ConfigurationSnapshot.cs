// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Service;

namespace Yarp.ReverseProxy.Configuration
{
    internal sealed class ConfigurationSnapshot : IProxyConfig
    {
        public List<RouteConfig> Routes { get; internal set; } = new List<RouteConfig>();

        public List<ClusterConfig> Clusters { get; internal set; } = new List<ClusterConfig>();

        IReadOnlyList<RouteConfig> IProxyConfig.Routes => Routes;

        IReadOnlyList<ClusterConfig> IProxyConfig.Clusters => Clusters;

        public IChangeToken ChangeToken { get; internal set; }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Kubernetes.Controller.Services;

internal class YarpConfigContext
{
    public Dictionary<string, ClusterTransfer> ClusterTransfers { get; set; } = new Dictionary<string, ClusterTransfer>();
    public List<RouteConfig> Routes { get; set; } = new List<RouteConfig>();

    public List<ClusterConfig> BuildClusterConfig()
    {
        return ClusterTransfers.Values.Select(c => new ClusterConfig() { Destinations = c.Destinations, ClusterId = c.ClusterId }).ToList();
    }
}

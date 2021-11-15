// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;
using Yarp.Kubernetes.Controller.Caching;

namespace Yarp.Kubernetes.Controller.Services;

internal sealed class YarpIngressContext
{
    public YarpIngressContext(IngressData ingress, List<ServiceData> services, List<Endpoints> endpoints)
    {
        Ingress = ingress;
        Services = services;
        Endpoints = endpoints;
    }

    public YarpIngressOptions Options { get; set; } = new YarpIngressOptions();
    public Dictionary<string, ClusterTransfer> ClusterTransfers { get; set; } = new Dictionary<string, ClusterTransfer>();
    public List<RouteConfig> Routes { get; set; } = new List<RouteConfig>();
    public List<ClusterConfig> Clusters { get; set; } = new List<ClusterConfig>();
    public IngressData Ingress { get; }
    public List<ServiceData> Services { get; }
    public List<Endpoints> Endpoints { get; }
}

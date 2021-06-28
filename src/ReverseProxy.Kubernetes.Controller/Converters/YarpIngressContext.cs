// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Kubernetes.Controller.Caching;

namespace Yarp.ReverseProxy.Kubernetes.Controller.Services
{
    internal sealed class YarpIngressContext
    {
        public YarpIngressContext(IngressData ingress, List<Endpoints> endpoints)
        {
            Ingress = ingress;
            Endpoints = endpoints;
        }

        public YarpIngressOptions Options { get; set; } = new YarpIngressOptions();
        public Dictionary<string, ClusterTransfer> ClusterTransfers { get; set; } = new Dictionary<string, ClusterTransfer>();
        public List<RouteConfig> Routes { get; set; } = new List<RouteConfig>();
        public List<ClusterConfig> Clusters { get; set; } = new List<ClusterConfig>();
        public IngressData Ingress { get; }
        public List<Endpoints> Endpoints { get; }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using IngressController.Caching;
using Microsoft.ReverseProxy.Abstractions;
using System.Collections.Generic;

namespace IngressController.Services
{
    internal class YarpIngressContext
    {
        public YarpIngressContext(IngressData ingress, List<Endpoints> endpoints)
        {
            Ingress = ingress;
            Endpoints = endpoints;
        }

        public YarpIngressOptions Options { get; set; } = new YarpIngressOptions();
        public Dictionary<string, ClusterTrasfer> ClusterTransfers { get; set; } = new Dictionary<string, ClusterTrasfer>();
        public List<ProxyRoute> Routes { get; set; } = new List<ProxyRoute>();
        public List<Cluster> Clusters { get; set; } = new List<Cluster>();
        public IngressData Ingress { get; }
        public List<Endpoints> Endpoints { get; }
    }
}

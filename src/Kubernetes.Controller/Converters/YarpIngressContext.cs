// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Yarp.Kubernetes.Controller.Caching;

namespace Yarp.Kubernetes.Controller.Converters;

internal sealed class YarpIngressContext
{
    public YarpIngressContext(IngressData ingress, List<ServiceData> services, List<Endpoints> endpoints)
    {
        Ingress = ingress;
        Services = services;
        Endpoints = endpoints;
    }

    public YarpIngressOptions Options { get; set; } = new YarpIngressOptions();
    public IngressData Ingress { get; }
    public List<ServiceData> Services { get; }
    public List<Endpoints> Endpoints { get; }
}

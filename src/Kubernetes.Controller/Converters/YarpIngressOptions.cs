// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Kubernetes.Controller.Converters;

internal sealed class YarpIngressOptions
{
    public bool Https { get; set; }
    public List<Dictionary<string, string>> Transforms { get; set; }
    public string AuthorizationPolicy { get; set; }
    public SessionAffinityConfig SessionAffinity { get; set; }
    public HttpClientConfig HttpClientConfig { get; set; }
    public string LoadBalancingPolicy { get; set; }
    public string CorsPolicy { get; set; }
    public HealthCheckConfig HealthCheck { get; set; }
    public Dictionary<string, string> RouteMetadata { get; set; }
    public List<RouteHeader> RouteHeaders { get; set; }
    public int? Order { get; set; }
}

internal sealed class RouteHeaderWapper
{
    public string Name { get; init; }
    public List<string> Values { get; init; }
    public HeaderMatchMode Mode { get; init; }
    public bool IsCaseSensitive { get; init; }

    public RouteHeader ToRouteHeader()
    {
        return new RouteHeader
        {
            Name = Name,
            Values = Values,
            Mode = Mode,
            IsCaseSensitive = IsCaseSensitive
        };
    }
}
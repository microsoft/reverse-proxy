// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Middleware
{
    internal static class HttpContextFeaturesExtensions
    {
        public static ClusterInfo GetRequiredCluster(this HttpContext context)
        {
            return context.Features.Get<ClusterInfo>() ?? throw new InvalidOperationException("Cluster unspecified.");
        }

        public static RouteConfig GetRequiredRouteConfig(this HttpContext context)
        {
            var endpoint = context.GetEndpoint()
               ?? throw new InvalidOperationException($"Routing Endpoint wasn't set for the current request.");

            var routeConfig = endpoint.Metadata.GetMetadata<RouteConfig>()
                ?? throw new InvalidOperationException($"Routing Endpoint is missing {typeof(RouteConfig).FullName} metadata.");

            return routeConfig;
        }

        public static IReverseProxyFeature GetReverseProxyFeature(this HttpContext context)
        {
            return context.Features.Get<IReverseProxyFeature>() ?? throw new InvalidOperationException("ReverseProxyFeature unspecified.");
        }
    }
}

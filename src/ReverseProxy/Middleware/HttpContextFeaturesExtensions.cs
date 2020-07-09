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
            var routeConfig = context.GetRequiredRouteConfig();
            var cluster = routeConfig.Cluster ?? throw new InvalidOperationException("Cluster unspecified.");
            return cluster;
        }

        public static RouteConfig GetRequiredRouteConfig(this HttpContext context)
        {
            var endpoint = context.GetEndpoint()
               ?? throw new InvalidOperationException($"Routing Endpoint wasn't set for the current request.");

            var routeConfig = endpoint.Metadata.GetMetadata<RouteConfig>()
                ?? throw new InvalidOperationException($"Routing Endpoint is missing {typeof(RouteConfig).FullName} metadata.");

            return routeConfig;
        }

        public static IReverseProxyFeature GetRequiredProxyFeature(this HttpContext context)
        {
            return context.Features.Get<IReverseProxyFeature>() ?? throw new InvalidOperationException("ReverseProxyFeature unspecified.");
        }
    }
}

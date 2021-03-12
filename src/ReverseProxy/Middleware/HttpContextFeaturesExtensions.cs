// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Middleware
{
    /// <summary>
    /// Extension methods for fetching proxy configuration from the current HttpContext.
    /// </summary>
    public static class HttpContextFeaturesExtensions
    {
        /// <summary>
        /// Retrieves the ClusterInfo instance associated with the current request.
        /// </summary>
        public static ClusterInfo GetRequiredCluster(this HttpContext context)
        {
            var routeConfig = context.GetRequiredRouteConfig();
            var cluster = routeConfig.Cluster ?? throw new InvalidOperationException("Cluster unspecified.");
            return cluster;
        }

        /// <summary>
        /// Retrieves the RouteConfig instance associated with the current request.
        /// </summary>
        public static RouteConfig GetRequiredRouteConfig(this HttpContext context)
        {
            var endpoint = context.GetEndpoint()
               ?? throw new InvalidOperationException($"Routing Endpoint wasn't set for the current request.");

            var routeConfig = endpoint.Metadata.GetMetadata<RouteConfig>()
                ?? throw new InvalidOperationException($"Routing Endpoint is missing {typeof(RouteConfig).FullName} metadata.");

            return routeConfig;
        }

        /// <summary>
        /// Retrieves the IReverseProxyFeature instance associated with the current request.
        /// </summary>
        public static IReverseProxyFeature GetRequiredProxyFeature(this HttpContext context)
        {
            return context.Features.Get<IReverseProxyFeature>() ?? throw new InvalidOperationException("ReverseProxyFeature unspecified.");
        }
    }
}

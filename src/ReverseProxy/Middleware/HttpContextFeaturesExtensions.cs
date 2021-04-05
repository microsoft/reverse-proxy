// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Yarp.ReverseProxy.Middleware;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service.Proxy;

namespace Microsoft.AspNetCore.Http
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
            // TODO: Retarget these to wrap GetRequiredProxyFeature
            var routeConfig = context.GetRequiredRouteConfig();
            var cluster = routeConfig.Cluster ?? throw new InvalidOperationException("Cluster unspecified.");
            return cluster;
        }

        /// <summary>
        /// Retrieves the RouteConfig instance associated with the current request.
        /// </summary>
        public static RouteConfig GetRequiredRouteConfig(this HttpContext context)
        {
            var proxyFeature = context.GetRequiredProxyFeature();

            var routeConfig = proxyFeature.RouteSnapshot
                ?? throw new InvalidOperationException($"Proxy feature is missing {typeof(RouteConfig).FullName}.");

            return routeConfig;
        }

        /// <summary>
        /// Retrieves the IReverseProxyFeature instance associated with the current request.
        /// </summary>
        public static IReverseProxyFeature GetRequiredProxyFeature(this HttpContext context)
        {
            return context.Features.Get<IReverseProxyFeature>() ?? throw new InvalidOperationException("ReverseProxyFeature unspecified.");
        }

        /// <summary>
        /// Retrieves the IProxyErrorFeature instance associated with the current request, if any.
        /// </summary>
        public static IProxyErrorFeature GetProxyErrorFeature(this HttpContext context)
        {
            return context.Features.Get<IProxyErrorFeature>();
        }
    }
}

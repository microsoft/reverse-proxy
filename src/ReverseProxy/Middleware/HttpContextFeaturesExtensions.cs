// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

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
        public static ClusterInfo GetClusterInfo(this HttpContext context)
        {
            var routeConfig = context.GetRouteConfig();
            var cluster = routeConfig.Cluster ?? throw new InvalidOperationException("Cluster unspecified.");
            return cluster;
        }

        /// <summary>
        /// Retrieves the RouteConfig instance associated with the current request.
        /// </summary>
        public static RouteConfig GetRouteConfig(this HttpContext context)
        {
            var proxyFeature = context.GetReverseProxyFeature();

            var routeConfig = proxyFeature.RouteSnapshot
                ?? throw new InvalidOperationException($"Proxy feature is missing {typeof(RouteConfig).FullName}.");

            return routeConfig;
        }

        /// <summary>
        /// Retrieves the IReverseProxyFeature instance associated with the current request.
        /// </summary>
        public static IReverseProxyFeature GetReverseProxyFeature(this HttpContext context)
        {
            return context.Features.Get<IReverseProxyFeature>() ?? throw new InvalidOperationException("ReverseProxyFeature unspecified.");
        }

        /// <summary>
        /// Retrieves the IProxyErrorFeature instance associated with the current request, if any.
        /// </summary>
        public static IProxyErrorFeature? GetProxyErrorFeature(this HttpContext context)
        {
            return context.Features.Get<IProxyErrorFeature>();
        }
    }
}

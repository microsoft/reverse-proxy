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
        /// Retrieves the <see cref="ClusterInfo"/> instance associated with the current request.
        /// </summary>
        public static ClusterInfo GetClusterInfo(this HttpContext context)
        {
            var routeModel = context.GetRouteModel();
            var cluster = routeModel.Cluster ?? throw new InvalidOperationException($"The {typeof(RouteModel).FullName} is missing the {typeof(ClusterInfo).FullName}.");
            return cluster;
        }

        /// <summary>
        /// Retrieves the <see cref="RouteModel"/> instance associated with the current request.
        /// </summary>
        public static RouteModel GetRouteModel(this HttpContext context)
        {
            var proxyFeature = context.GetReverseProxyFeature();

            var routeModel = proxyFeature.Route
                ?? throw new InvalidOperationException($"The {typeof(IReverseProxyFeature).FullName} is missing the {typeof(RouteModel).FullName}.");

            return routeModel;
        }

        /// <summary>
        /// Retrieves the <see cref="IReverseProxyFeature"/> instance associated with the current request.
        /// </summary>
        public static IReverseProxyFeature GetReverseProxyFeature(this HttpContext context)
        {
            return context.Features.Get<IReverseProxyFeature>() ?? throw new InvalidOperationException($"{typeof(IReverseProxyFeature).FullName} is missing.");
        }

        /// <summary>
        /// Retrieves the <see cref="IProxyErrorFeature"/> instance associated with the current request, if any.
        /// </summary>
        public static IProxyErrorFeature? GetProxyErrorFeature(this HttpContext context)
        {
            return context.Features.Get<IProxyErrorFeature>();
        }
    }
}

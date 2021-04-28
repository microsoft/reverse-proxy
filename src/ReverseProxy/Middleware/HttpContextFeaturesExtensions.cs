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
            var routeState = context.GetRouteState();
            var cluster = routeState.Cluster ?? throw new InvalidOperationException($"The {typeof(RouteState).FullName} is missing the {typeof(ClusterInfo).FullName}.");
            return cluster;
        }

        /// <summary>
        /// Retrieves the <see cref="RouteState"/> instance associated with the current request.
        /// </summary>
        public static RouteState GetRouteState(this HttpContext context)
        {
            var proxyFeature = context.GetReverseProxyFeature();

            var routeState = proxyFeature.RouteState
                ?? throw new InvalidOperationException($"The {typeof(IReverseProxyFeature).FullName} is missing the {typeof(RouteState).FullName}.");

            return routeState;
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

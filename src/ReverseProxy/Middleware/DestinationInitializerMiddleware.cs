// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Middleware
{
    /// <summary>
    /// Initializes the proxy processing pipeline with the available healthy destinations.
    /// </summary>
    internal class DestinationInitializerMiddleware
    {
        private readonly ILogger _logger;
        private readonly RequestDelegate _next;

        public DestinationInitializerMiddleware(RequestDelegate next,
            ILogger<DestinationInitializerMiddleware> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public Task Invoke(HttpContext context)
        {
            var routeConfig = context.GetRequiredRouteConfig();

            var cluster = routeConfig.Cluster;
            // TODO: Validate on load https://github.com/microsoft/reverse-proxy/issues/797
            if (cluster == null)
            {
                Log.NoClusterFound(_logger, routeConfig.ProxyRoute.RouteId);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return Task.CompletedTask;
            }

            var dynamicState = cluster.DynamicState;
            context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature
            {
                RouteSnapshot = routeConfig,
                ClusterSnapshot = cluster.Config,
                AllDestinations = dynamicState.AllDestinations,
                AvailableDestinations = dynamicState.HealthyDestinations
            });

            return _next(context);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _noClusterFound = LoggerMessage.Define<string>(
                LogLevel.Information,
                EventIds.NoClusterFound,
                "Route '{routeId}' has no cluster information.");

            public static void NoClusterFound(ILogger logger, string routeId)
            {
                _noClusterFound(logger, routeId, null);
            }
        }
    }
}

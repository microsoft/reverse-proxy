// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Middleware
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
            if (cluster == null)
            {
                Log.NoClusterFound(_logger, routeConfig.Route.RouteId);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return Task.CompletedTask;
            }

            var clusterConfig = cluster.Config;
            if (clusterConfig == null)
            {
                Log.ClusterConfigNotAvailable(_logger, routeConfig.Route.RouteId, cluster.ClusterId);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return Task.CompletedTask;
            }

            var dynamicState = cluster.DynamicState;
            if (dynamicState == null)
            {
                Log.ClusterDataNotAvailable(_logger, routeConfig.Route.RouteId, cluster.ClusterId);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return Task.CompletedTask;
            }

            if (dynamicState.HealthyDestinations.Count == 0)
            {
                Log.NoHealthyDestinations(_logger, routeConfig.Route.RouteId, cluster.ClusterId);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return Task.CompletedTask;
            }

            context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature
            {
                ClusterConfig = clusterConfig,
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

            private static readonly Action<ILogger, string, string, Exception> _clusterDataNotAvailable = LoggerMessage.Define<string, string>(
                LogLevel.Information,
                EventIds.ClusterDataNotAvailable,
                "Route '{routeId}' has no up to date information on its cluster '{clusterId}'. " +
                "Perhaps the cluster hasn't been probed yet? " +
                "This can happen when a new cluster is added but isn't ready to serve traffic yet.");

            private static readonly Action<ILogger, string, string, Exception> _clusterConfigNotAvailable = LoggerMessage.Define<string, string>(
                LogLevel.Information,
                EventIds.ClusterConfigNotAvailable,
                "Route '{routeId}' has no config on its cluster '{clusterId}'.");

            private static readonly Action<ILogger, string, string, Exception> _noHealthyDestinations = LoggerMessage.Define<string, string>(
                LogLevel.Information,
                EventIds.NoHealthyDestinations,
                "Route '{routeId}' has no available healthy destinations for Cluster '{clusterId}'.");

            public static void NoClusterFound(ILogger logger, string routeId)
            {
                _noClusterFound(logger, routeId, null);
            }

            public static void ClusterDataNotAvailable(ILogger logger, string routeId, string clusterId)
            {
                _clusterDataNotAvailable(logger, routeId, clusterId, null);
            }

            public static void ClusterConfigNotAvailable(ILogger logger, string routeId, string clusterId)
            {
                _clusterConfigNotAvailable(logger, routeId, clusterId, null);
            }

            public static void NoHealthyDestinations(ILogger logger, string routeId, string clusterId)
            {
                _noHealthyDestinations(logger, routeId, clusterId, null);
            }
        }
    }
}

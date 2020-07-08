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
            var endpoint = context.GetEndpoint()
                ?? throw new InvalidOperationException($"Routing Endpoint wasn't set for the current request.");

            var routeConfig = endpoint.Metadata.GetMetadata<RouteConfig>()
                ?? throw new InvalidOperationException($"Routing Endpoint is missing {typeof(RouteConfig).FullName} metadata.");

            var cluster = routeConfig.Cluster;
            if (cluster == null)
            {
                Log.NoClusterFound(_logger, routeConfig.Route.RouteId);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return Task.CompletedTask;
            }

            var clusterConfig = cluster.Config.Value
                ?? throw new InvalidOperationException($"Cluster Config unspecified.");

            var dynamicState = cluster.DynamicState.Value;
            if (dynamicState == null)
            {
                Log.ClusterDataNotAvailable(_logger, routeConfig.Route.RouteId, cluster.ClusterId);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return Task.CompletedTask;
            }

            if (dynamicState.HealthyDestinations.Count == 0)
            {
                Log.NoHealthyDestinations(_logger, routeConfig.Route.RouteId, cluster.ClusterId);
                context.Response.StatusCode = 503;
                return Task.CompletedTask;
            }

            context.Features.Set(cluster);
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
                "Route `{routeId}` has no cluster information.");

            private static readonly Action<ILogger, string, string, Exception> _clusterDataNotAvailable = LoggerMessage.Define<string, string>(
                LogLevel.Information,
                EventIds.ClusterDataNotAvailable,
                "Route `{routeId}` has no up to date information on its cluster '{clusterId}'. " +
                "Perhaps the cluster hasn't been probed yet? " +
                "This can happen when a new cluster is added but isn't ready to serve traffic yet.");

            private static readonly Action<ILogger, string, string, Exception> _noHealthyDestinations = LoggerMessage.Define<string, string>(
                LogLevel.Information,
                EventIds.NoHealthyDestinations,
                "Route `{routeId}` has no available healthy destinations for Cluster `{clusterId}`.");

            public static void NoClusterFound(ILogger logger, string routeId)
            {
                _noClusterFound(logger, routeId, null);
            }

            public static void ClusterDataNotAvailable(ILogger logger, string routeId, string clusterId)
            {
                _clusterDataNotAvailable(logger, routeId, clusterId, null);
            }

            public static void NoHealthyDestinations(ILogger logger, string routeId, string clusterId)
            {
                _noHealthyDestinations(logger, routeId, clusterId, null);
            }
        }
    }
}

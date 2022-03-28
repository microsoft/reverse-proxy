// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Model;

/// <summary>
/// Initializes the proxy processing pipeline with the available healthy destinations.
/// </summary>
internal sealed class ProxyPipelineInitializerMiddleware
{
    private readonly ILogger _logger;
    private readonly RequestDelegate _next;

    public ProxyPipelineInitializerMiddleware(RequestDelegate next,
        ILogger<ProxyPipelineInitializerMiddleware> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public Task Invoke(HttpContext context)
    {
        var endpoint = context.GetEndpoint()
           ?? throw new InvalidOperationException($"Routing Endpoint wasn't set for the current request.");

        var route = endpoint.Metadata.GetMetadata<RouteModel>()
            ?? throw new InvalidOperationException($"Routing Endpoint is missing {typeof(RouteModel).FullName} metadata.");

        var cluster = route.Cluster;
        // TODO: Validate on load https://github.com/microsoft/reverse-proxy/issues/797
        if (cluster is null)
        {
            Log.NoClusterFound(_logger, route.Config.RouteId);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        }

        var destinationsState = cluster.DestinationsState;
        context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature
        {
            Route = route,
            Cluster = cluster.Model,
            AllDestinations = destinationsState.AllDestinations,
            AvailableDestinations = destinationsState.AvailableDestinations
        });

        return _next(context);
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _noClusterFound = LoggerMessage.Define<string>(
            LogLevel.Information,
            EventIds.NoClusterFound,
            "Route '{routeId}' has no cluster information.");

        public static void NoClusterFound(ILogger logger, string routeId)
        {
            _noClusterFound(logger, routeId, null);
        }
    }
}

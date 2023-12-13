// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Http.Timeouts;
#endif
using Microsoft.Extensions.Logging;
#if NET8_0_OR_GREATER
using Yarp.ReverseProxy.Configuration;
#endif
using Yarp.ReverseProxy.Utilities;

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

        var weightClusters = route.WeightClusters;

        if (weightClusters.Any())
        {
            route.SetCluster(PickCluster(weightClusters));
        }
        var cluster = route.Cluster;
        // TODO: Validate on load https://github.com/microsoft/reverse-proxy/issues/797
        if (cluster is null)
        {
            Log.NoClusterFound(_logger, route.Config.RouteId);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        }
#if NET8_0_OR_GREATER
        // There's no way to detect the presence of the timeout middleware before this, only the options.
        if (endpoint.Metadata.GetMetadata<RequestTimeoutAttribute>() != null
            && context.Features.Get<IHttpRequestTimeoutFeature>() == null
            // The feature is skipped if the request is already canceled. We'll handle canceled requests later for consistency.
            && !context.RequestAborted.IsCancellationRequested)
        {
            Log.TimeoutNotApplied(_logger, route.Config.RouteId);
            // Out of an abundance of caution, refuse the request rather than allowing it to proceed without the configured timeout.
            throw new InvalidOperationException($"The timeout was not applied for route '{route.Config.RouteId}', ensure `IApplicationBuilder.UseRequestTimeouts()`"
                + " is called between `IApplicationBuilder.UseRouting()` and `IApplicationBuilder.UseEndpoints()`.");
        }
#endif
        var destinationsState = cluster.DestinationsState;
        context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature
        {
            Route = route,
            Cluster = cluster.Model,
            AllDestinations = destinationsState.AllDestinations,
            AvailableDestinations = destinationsState.AvailableDestinations,
        });

        var activity = Observability.YarpActivitySource.CreateActivity("proxy.forwarder", ActivityKind.Server);

        return activity is null
            ? _next(context)
            : AwaitWithActivity(context, activity);
    }

    private async Task AwaitWithActivity(HttpContext context, Activity activity)
    {
        context.SetYarpActivity(activity);

        activity.Start();
        try
        {
            await _next(context);
        }
        finally
        {
            activity.Dispose();
        }
    }

    private ClusterState PickCluster(WeightedList<ClusterState> clusters)
    {
        return clusters.Next();
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _noClusterFound = LoggerMessage.Define<string>(
            LogLevel.Information,
            EventIds.NoClusterFound,
            "Route '{routeId}' has no cluster information.");

        private static readonly Action<ILogger, string, Exception?> _timeoutNotApplied = LoggerMessage.Define<string>(
            LogLevel.Error,
            EventIds.TimeoutNotApplied,
            "The timeout was not applied for route '{routeId}', ensure `IApplicationBuilder.UseRequestTimeouts()` is called between `IApplicationBuilder.UseRouting()` and `IApplicationBuilder.UseEndpoints()`.");

        public static void NoClusterFound(ILogger logger, string routeId)
        {
            _noClusterFound(logger, routeId, null);
        }

        public static void TimeoutNotApplied(ILogger logger, string routeId)
        {
            _timeoutNotApplied(logger, routeId, null);
        }
    }
}

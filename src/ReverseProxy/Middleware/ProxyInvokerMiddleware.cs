// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Telemetry;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Middleware
{
    /// <summary>
    /// Invokes the proxy at the end of the request processing pipeline.
    /// </summary>
    internal class ProxyInvokerMiddleware
    {
        private readonly IRandomFactory _randomFactory;
        private readonly RequestDelegate _next; // Unused, this middleware is always terminal
        private readonly ILogger _logger;
        private readonly IHttpProxy _httpProxy;

        public ProxyInvokerMiddleware(
            RequestDelegate next,
            ILogger<ProxyInvokerMiddleware> logger,
            IHttpProxy httpProxy,
            IRandomFactory randomFactory)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpProxy = httpProxy ?? throw new ArgumentNullException(nameof(httpProxy));
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
        }

        /// <inheritdoc/>
        public async Task Invoke(HttpContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            var reverseProxyFeature = context.GetRequiredProxyFeature();
            var destinations = reverseProxyFeature.AvailableDestinations
                ?? throw new InvalidOperationException($"The {nameof(IReverseProxyFeature)} Destinations collection was not set.");

            var routeConfig = context.GetRequiredRouteConfig();
            var cluster = routeConfig.Cluster;

            if (destinations.Count == 0)
            {
                Log.NoAvailableDestinations(_logger, cluster.ClusterId);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Features.Set<IProxyErrorFeature>(new ProxyErrorFeature(ProxyError.NoAvailableDestinations, ex: null));
                return;
            }

            var destination = destinations[0];
            if (destinations.Count > 1)
            {
                var random = _randomFactory.CreateRandomInstance();
                Log.MultipleDestinationsAvailable(_logger, cluster.ClusterId);
                destination = destinations[random.Next(destinations.Count)];
            }

            reverseProxyFeature.SelectedDestination = destination;

            var destinationConfig = destination.Config;
            if (destinationConfig == null)
            {
                throw new InvalidOperationException($"Chosen destination has no configs set: '{destination.DestinationId}'");
            }

            try
            {
                cluster.ConcurrencyCounter.Increment();
                destination.ConcurrencyCounter.Increment();

                ProxyTelemetry.Log.ProxyInvoke(cluster.ClusterId, routeConfig.Route.RouteId, destination.DestinationId);

                var clusterConfig = reverseProxyFeature.ClusterConfig;
                await _httpProxy.ProxyAsync(context, destinationConfig.Options.Address, clusterConfig.HttpClient, clusterConfig.Options.HttpRequest, routeConfig.Transformer);
            }
            finally
            {
                destination.ConcurrencyCounter.Decrement();
                cluster.ConcurrencyCounter.Decrement();
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _noAvailableDestinations = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.NoAvailableDestinations,
                "No available destinations after load balancing for cluster '{clusterId}'.");

            private static readonly Action<ILogger, string, Exception> _multipleDestinationsAvailable = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.MultipleDestinationsAvailable,
                "More than one destination available for cluster '{clusterId}', load balancing may not be configured correctly. Choosing randomly.");

            public static void NoAvailableDestinations(ILogger logger, string clusterId)
            {
                _noAvailableDestinations(logger, clusterId, null);
            }

            public static void MultipleDestinationsAvailable(ILogger logger, string clusterId)
            {
                _multipleDestinationsAvailable(logger, clusterId, null);
            }
        }
    }
}

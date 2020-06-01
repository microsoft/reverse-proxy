// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Middleware
{
    /// <summary>
    /// Invokes the proxy at the end of the request processing pipeline.
    /// </summary>
    internal class ProxyInvokerMiddleware
    {
        private readonly IRandomFactory _randomFactory = new RandomFactory();
        private readonly RequestDelegate _next; // Unused, this middleware is always terminal
        private readonly ILogger _logger;
        private readonly IOperationLogger<ProxyInvokerMiddleware> _operationLogger;
        private readonly IHttpProxy _httpProxy;

        public ProxyInvokerMiddleware(
            RequestDelegate next,
            ILogger<ProxyInvokerMiddleware> logger,
            IOperationLogger<ProxyInvokerMiddleware> operationLogger,
            IHttpProxy httpProxy)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationLogger = operationLogger ?? throw new ArgumentNullException(nameof(operationLogger));
            _httpProxy = httpProxy ?? throw new ArgumentNullException(nameof(httpProxy));
        }

        /// <inheritdoc/>
        public async Task Invoke(HttpContext context)
        {
            Contracts.CheckValue(context, nameof(context));

            var backend = context.Features.Get<BackendInfo>() ?? throw new InvalidOperationException("Backend unspecified.");
            var destinations = context.Features.Get<IAvailableDestinationsFeature>()?.Destinations
                ?? throw new InvalidOperationException("The IAvailableDestinationsFeature Destinations collection was not set.");
            var routeConfig = context.GetEndpoint()?.Metadata.GetMetadata<RouteConfig>()
                ?? throw new InvalidOperationException("RouteConfig unspecified.");

            if (destinations.Count == 0)
            {
                Log.NoAvailableDestinations(_logger, backend.BackendId);
                context.Response.StatusCode = 503;
                return;
            }

            var destination = destinations[0];
            if (destinations.Count > 1)
            {
                var random = _randomFactory.CreateRandomInstance();
                Log.MultipleDestinationsAvailable(_logger, backend.BackendId);
                destination = destinations[random.Next(destinations.Count)];
            }

            var destinationConfig = destination.Config.Value;
            if (destinationConfig == null)
            {
                throw new InvalidOperationException($"Chosen destination has no configs set: '{destination.DestinationId}'");
            }

            // TODO: support StripPrefix and other url transformations
            var targetUrl = BuildOutgoingUrl(context, destinationConfig.Address);
            Log.Proxying(_logger, targetUrl);
            var targetUri = new Uri(targetUrl, UriKind.Absolute);

            using (var shortCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted))
            {
                // TODO: Configurable timeout, measure from request start, make it unit-testable
                shortCts.CancelAfter(TimeSpan.FromSeconds(30));

                // TODO: Retry against other destinations
                try
                {
                    // TODO: Apply caps
                    backend.ConcurrencyCounter.Increment();
                    destination.ConcurrencyCounter.Increment();

                    // TODO: Duplex channels should not have a timeout (?), but must react to Proxy force-shutdown signals.
                    var longCancellation = context.RequestAborted;

                    var proxyTelemetryContext = new ProxyTelemetryContext(
                        backendId: backend.BackendId,
                        routeId: routeConfig.Route.RouteId,
                        destinationId: destination.DestinationId);

                    await _operationLogger.ExecuteAsync(
                        "ReverseProxy.Proxy",
                        () => _httpProxy.ProxyAsync(context, targetUri, backend.ProxyHttpClientFactory, proxyTelemetryContext, shortCancellation: shortCts.Token, longCancellation: longCancellation));
                }
                finally
                {
                    destination.ConcurrencyCounter.Decrement();
                    backend.ConcurrencyCounter.Decrement();
                }
            }
        }

        private string BuildOutgoingUrl(HttpContext context, string destinationAddress)
        {
            // "http://a".Length = 8
            if (destinationAddress == null || destinationAddress.Length < 8)
            {
                throw new ArgumentException(nameof(destinationAddress));
            }

            var stripSlash = destinationAddress.EndsWith('/');

            // NOTE: This takes inspiration from Microsoft.AspNetCore.Http.Extensions.UriHelper.BuildAbsolute()
            var request = context.Request;
            var combinedPath = (request.PathBase.HasValue || request.Path.HasValue) ? (request.PathBase + request.Path).ToString() : "/";
            var encodedQuery = request.QueryString.ToString();

            // PERF: Calculate string length to allocate correct buffer size for StringBuilder.
            var length = destinationAddress.Length + combinedPath.Length + encodedQuery.Length + (stripSlash ? -1 : 0);

            var builder = new StringBuilder(length);
            if (stripSlash)
            {
                builder.Append(destinationAddress, 0, destinationAddress.Length - 1);
            }
            else
            {
                builder.Append(destinationAddress);
            }

            builder.Append(combinedPath);
            builder.Append(encodedQuery);

            return builder.ToString();
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _noAvailableDestinations = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.NoAvailableDestinations,
                "No available destinations after load balancing for backend `{backendId}`.");

            private static readonly Action<ILogger, string, Exception> _multipleDestinationsAvailable = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.MultipleDestinationsAvailable,
                "More than one destination available for backend `{backendId}`, load balancing may not be configured correctly. Choosing randomly.");

            private static readonly Action<ILogger, string, Exception> _proxying = LoggerMessage.Define<string>(
                LogLevel.Information,
                EventIds.Proxying,
                "Proxying to {targetUrl}");

            public static void NoAvailableDestinations(ILogger logger, string backendId)
            {
                _noAvailableDestinations(logger, backendId, null);
            }

            public static void MultipleDestinationsAvailable(ILogger logger, string backendId)
            {
                _multipleDestinationsAvailable(logger, backendId, null);
            }

            public static void Proxying(ILogger logger, string targetUrl)
            {
                _proxying(logger, targetUrl, null);
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Common;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Core.Service.Proxy;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.Middleware
{
    /// <summary>
    /// Invokes the proxy at the end of the request processing pipeline.
    /// </summary>
    internal class ProxyInvokerMiddleware
    {
        private readonly Random _random = new Random();
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
            var endpoints = context.Features.Get<IAvailableBackendEndpointsFeature>()?.Endpoints
                ?? throw new InvalidOperationException("The AvailableBackendEndpoints collection was not set.");
            var routeConfig = context.GetEndpoint()?.Metadata.GetMetadata<RouteConfig>()
                ?? throw new InvalidOperationException("RouteConfig unspecified.");

            if (endpoints.Count == 0)
            {
                Log.NoAvailableEndpoints(_logger, backend.BackendId);
                context.Response.StatusCode = 503;
                return;
            }

            var endpoint = endpoints[0];
            if (endpoints.Count > 1)
            {
                Log.MultipleEndpointsAvailable(_logger, backend.BackendId);
                endpoint = endpoints[_random.Next(endpoints.Count)];
            }

            var endpointConfig = endpoint.Config.Value;
            if (endpointConfig == null)
            {
                throw new InvalidOperationException($"Chosen endpoint has no configs set: '{endpoint.EndpointId}'");
            }

            // TODO: support StripPrefix and other url transformations
            var targetUrl = BuildOutgoingUrl(context, endpointConfig.Address);
            Log.Proxying(_logger, targetUrl);
            var targetUri = new Uri(targetUrl, UriKind.Absolute);

            using (var shortCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted))
            {
                // TODO: Configurable timeout, measure from request start, make it unit-testable
                shortCts.CancelAfter(TimeSpan.FromSeconds(30));

                // TODO: Retry against other endpoints
                try
                {
                    // TODO: Apply caps
                    backend.ConcurrencyCounter.Increment();
                    endpoint.ConcurrencyCounter.Increment();

                    // TODO: Duplex channels should not have a timeout (?), but must react to Proxy force-shutdown signals.
                    var longCancellation = context.RequestAborted;

                    var proxyTelemetryContext = new ProxyTelemetryContext(
                        backendId: backend.BackendId,
                        routeId: routeConfig.Route.RouteId,
                        endpointId: endpoint.EndpointId);

                    await _operationLogger.ExecuteAsync(
                        "ReverseProxy.Proxy",
                        () => _httpProxy.ProxyAsync(context, targetUri, backend.ProxyHttpClientFactory, proxyTelemetryContext, shortCancellation: shortCts.Token, longCancellation: longCancellation));
                }
                finally
                {
                    endpoint.ConcurrencyCounter.Decrement();
                    backend.ConcurrencyCounter.Decrement();
                }
            }
        }

        private string BuildOutgoingUrl(HttpContext context, string endpointAddress)
        {
            // "http://a".Length = 8
            if (endpointAddress == null || endpointAddress.Length < 8)
            {
                throw new ArgumentException(nameof(endpointAddress));
            }

            var stripSlash = endpointAddress.EndsWith('/');

            // NOTE: This takes inspiration from Microsoft.AspNetCore.Http.Extensions.UriHelper.BuildAbsolute()
            var request = context.Request;
            var combinedPath = (request.PathBase.HasValue || request.Path.HasValue) ? (request.PathBase + request.Path).ToString() : "/";
            var encodedQuery = request.QueryString.ToString();

            // PERF: Calculate string length to allocate correct buffer size for StringBuilder.
            var length = endpointAddress.Length + combinedPath.Length + encodedQuery.Length + (stripSlash ? -1 : 0);

            var builder = new StringBuilder(length);
            if (stripSlash)
            {
                builder.Append(endpointAddress, 0, endpointAddress.Length - 1);
            }
            else
            {
                builder.Append(endpointAddress);
            }

            builder.Append(combinedPath);
            builder.Append(encodedQuery);

            return builder.ToString();
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _noAvailableEndpoints = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.NoAvailableEndpoints,
                "No available endpoints after load balancing for backend `{backendId}`.");

            private static readonly Action<ILogger, string, Exception> _multipleEndpointsAvailable = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.MultipleEndpointsAvailable,
                "More than one endpoint available for backend `{backendId}`, load balancing may not be configured correctly. Choosing randomly.");

            private static readonly Action<ILogger, string, Exception> _proxying = LoggerMessage.Define<string>(
                LogLevel.Information,
                EventIds.Proxying,
                "Proxying to {targetUrl}");

            public static void NoAvailableEndpoints(ILogger logger, string backendId)
            {
                _noAvailableEndpoints(logger, backendId, null);
            }

            public static void MultipleEndpointsAvailable(ILogger logger, string backendId)
            {
                _multipleEndpointsAvailable(logger, backendId, null);
            }

            public static void Proxying(ILogger logger, string targetUrl)
            {
                _proxying(logger, targetUrl, null);
            }
        }
    }
}

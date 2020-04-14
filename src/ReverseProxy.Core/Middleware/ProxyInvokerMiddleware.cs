// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
        private readonly IOperationLogger _operationLogger;
        private readonly IHttpProxy _httpProxy;

        public ProxyInvokerMiddleware(
            RequestDelegate next,
            ILogger<ProxyInvokerMiddleware> logger,
            IOperationLogger operationLogger,
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

            var aspNetCoreEndpoint = context.GetEndpoint();
            var routeConfig = aspNetCoreEndpoint.Metadata.GetMetadata<RouteConfig>();
            var backend = routeConfig.BackendOrNull;

            var endpoints = context.Features.Get<IAvailableBackendEndpointsFeature>()?.Endpoints
                ?? throw new InvalidOperationException("The AvailableBackendEndpoints collection was not set.");

            if (endpoints.Count == 0)
            {
                _logger.LogInformation("No available endpoints.");
                context.Response.StatusCode = 503;
                return;
            }

            if (endpoints.Count > 1)
            {
                _logger.LogWarning("More than one endpoint available, load balancing may not be configured correctly. Choosing randomly.");
            }

            var endpoint = endpoints[_random.Next(endpoints.Count)];

            var endpointConfig = endpoint.Config.Value;
            if (endpointConfig == null)
            {
                throw new InvalidOperationException($"Chosen endpoint has no configs set: '{endpoint.EndpointId}'");
            }

            // TODO: support StripPrefix and other url transformations
            var targetUrl = BuildOutgoingUrl(context, endpointConfig.Address);
            _logger.LogInformation($"Proxying to {targetUrl}");
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
    }
}

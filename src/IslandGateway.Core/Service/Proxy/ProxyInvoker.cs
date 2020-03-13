// <copyright file="ProxyInvoker.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Common.Abstractions.Telemetry;
using IslandGateway.Core.Abstractions;
using IslandGateway.Core.RuntimeModel;
using IslandGateway.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace IslandGateway.Core.Service.Proxy
{
    /// <summary>
    /// Default implementation of <see cref="IProxyInvoker"/>.
    /// </summary>
    internal class ProxyInvoker : IProxyInvoker
    {
        private readonly ILogger<ProxyInvoker> _logger;
        private readonly IOperationLogger _operationLogger;
        private readonly ILoadBalancer _loadBalancer;
        private readonly IHttpProxy _httpProxy;

        public ProxyInvoker(
            ILogger<ProxyInvoker> logger,
            IOperationLogger operationLogger,
            ILoadBalancer loadBalancer,
            IHttpProxy httpProxy)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(operationLogger, nameof(operationLogger));
            Contracts.CheckValue(loadBalancer, nameof(loadBalancer));
            Contracts.CheckValue(httpProxy, nameof(httpProxy));

            this._logger = logger;
            this._operationLogger = operationLogger;
            this._loadBalancer = loadBalancer;
            this._httpProxy = httpProxy;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(HttpContext context)
        {
            Contracts.CheckValue(context, nameof(context));

            var aspNetCoreEndpoint = context.GetEndpoint();
            if (aspNetCoreEndpoint == null)
            {
                throw new GatewayException($"ASP .NET Core Endpoint wasn't set for the current request. This is a coding defect.");
            }

            var routeConfig = aspNetCoreEndpoint.Metadata.GetMetadata<RouteConfig>();
            if (routeConfig == null)
            {
                throw new GatewayException($"ASP .NET Core Endpoint is missing {typeof(RouteConfig).FullName} metadata. This is a coding defect.");
            }

            var backend = routeConfig.BackendOrNull;
            if (backend == null)
            {
                throw new GatewayException($"Route has no backend information.");
            }

            var dynamicState = backend.DynamicState.Value;
            if (dynamicState == null)
            {
                throw new GatewayException($"Route has no up to date information on its backend '{backend.BackendId}'. Perhaps the backend hasn't been probed yet? This can happen when a new backend is added but isn't ready to serve traffic yet.");
            }

            // TODO: Set defaults properly
            BackendConfig.BackendLoadBalancingOptions loadBalancingOptions = default;
            var backendConfig = backend.Config.Value;
            if (backendConfig != null)
            {
                loadBalancingOptions = backendConfig.LoadBalancingOptions;
            }

            var endpoint = this._operationLogger.Execute(
                "IslandGateway.PickEndpoint",
                () => this._loadBalancer.PickEndpoint(dynamicState.HealthyEndpoints, dynamicState.AllEndpoints, in loadBalancingOptions));

            if (endpoint == null)
            {
                throw new GatewayException($"No available endpoints.");
            }

            var endpointConfig = endpoint.Config.Value;
            if (endpointConfig == null)
            {
                throw new GatewayException($"Chosen endpoint has no configs set: '{endpoint.EndpointId}'");
            }

            // TODO: support StripPrefix and other url transformations
            var targetUrl = this.BuildOutgoingUrl(context, endpointConfig.Address);
            this._logger.LogInformation($"Proxying to {targetUrl}");
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

                    // TODO: Duplex channels should not have a timeout (?), but must react to Gateway force-shutdown signals.
                    var longCancellation = context.RequestAborted;

                    var proxyTelemetryContext = new ProxyTelemetryContext(
                        backendId: backend.BackendId,
                        routeId: routeConfig.Route.RouteId,
                        endpointId: endpoint.EndpointId);

                    await this._operationLogger.ExecuteAsync(
                        "IslandGateway.Proxy",
                        () => this._httpProxy.ProxyAsync(context, targetUri, backend.ProxyHttpClientFactory, proxyTelemetryContext, shortCancellation: shortCts.Token, longCancellation: longCancellation));
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

            bool stripSlash = endpointAddress.EndsWith('/');

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

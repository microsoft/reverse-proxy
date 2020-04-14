// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Core.Service.Proxy;

namespace Microsoft.ReverseProxy.Core.Middleware
{
    /// <summary>
    /// Load balances across the available endpoints.
    /// </summary>
    public class LoadBalancingMiddleware
    {
        private readonly ILogger _logger;
        private readonly IOperationLogger _operationLogger;
        private readonly ILoadBalancer _loadBalancer;
        private readonly RequestDelegate _next;

        public LoadBalancingMiddleware(
            RequestDelegate next,
            ILogger<LoadBalancingMiddleware> logger,
            IOperationLogger operationLogger,
            ILoadBalancer loadBalancer)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationLogger = operationLogger ?? throw new ArgumentNullException(nameof(operationLogger));
            _loadBalancer = loadBalancer ?? throw new ArgumentNullException(nameof(loadBalancer));
        }

        public Task Invoke(HttpContext context)
        { 
            var backend = context.Features.Get<BackendInfo>() ?? throw new InvalidOperationException("Backend unspecified.");
            var endpointsFeature = context.Features.Get<IAvailableBackendEndpointsFeature>();
            var endpoints = endpointsFeature?.Endpoints
                ?? throw new InvalidOperationException("The AvailableBackendEndpoints collection was not set.");

            // TODO: Set defaults properly
            var loadBalancingOptions = backend.Config.Value?.LoadBalancingOptions ?? default;

            var endpoint = _operationLogger.Execute(
                "ReverseProxy.PickEndpoint",
                () => _loadBalancer.PickEndpoint(endpoints, in loadBalancingOptions));

            if (endpoint == null)
            {
                _logger.LogDebug($"No available endpoints after load balancing.");
                context.Response.StatusCode = 503;
                return Task.CompletedTask;
            }

            endpointsFeature.Endpoints = new List<EndpointInfo>() { endpoint }.AsReadOnly();

            return _next(context);
        }
    }
}

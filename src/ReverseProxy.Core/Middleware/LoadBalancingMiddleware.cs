// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Common;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Core.Service.Proxy;

namespace Microsoft.ReverseProxy.Core.Middleware
{
    /// <summary>
    /// Load balances across the available endpoints.
    /// </summary>
    internal class LoadBalancingMiddleware
    {
        private readonly ILogger _logger;
        private readonly IOperationLogger<LoadBalancingMiddleware> _operationLogger;
        private readonly ILoadBalancer _loadBalancer;
        private readonly RequestDelegate _next;

        public LoadBalancingMiddleware(
            RequestDelegate next,
            ILogger<LoadBalancingMiddleware> logger,
            IOperationLogger<LoadBalancingMiddleware> operationLogger,
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

            var loadBalancingOptions = backend.Config.Value?.LoadBalancingOptions
                ?? new BackendConfig.BackendLoadBalancingOptions(default);

            var endpoint = _operationLogger.Execute(
                "ReverseProxy.PickEndpoint",
                () => _loadBalancer.PickEndpoint(endpoints, in loadBalancingOptions));

            if (endpoint == null)
            {
                Log.NoAvailableEndpoints(_logger, backend.BackendId);
                context.Response.StatusCode = 503;
                return Task.CompletedTask;
            }

            endpointsFeature.Endpoints = new[] { endpoint };

            return _next(context);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _noAvailableEndpoints = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.NoAvailableEndpoints,
                "No available endpoints after load balancing for backend `{backendId}`.");

            public static void NoAvailableEndpoints(ILogger logger, string backendId)
            {
                _noAvailableEndpoints(logger, backendId, null);
            }
        }
    }
}

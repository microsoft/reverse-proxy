// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace Microsoft.ReverseProxy.Core.Middleware
{
    /// <summary>
    /// Initializes the proxy processing pipeline with the available healthy endpoints.
    /// </summary>
    internal class EndpointInitializerMiddleware
    {
        private readonly ILogger _logger;
        private readonly RequestDelegate _next;

        public EndpointInitializerMiddleware(RequestDelegate next,
            ILogger<EndpointInitializerMiddleware> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public Task Invoke(HttpContext context)
        {
            var aspNetCoreEndpoint = context.GetEndpoint()
                ?? throw new InvalidOperationException($"Routing Endpoint wasn't set for the current request.");

            var routeConfig = aspNetCoreEndpoint.Metadata.GetMetadata<RouteConfig>()
                ?? throw new InvalidOperationException($"Routing Endpoint is missing {typeof(RouteConfig).FullName} metadata.");

            var backend = routeConfig.BackendOrNull;
            if (backend == null)
            {
                _logger.LogInformation("Route {routeId} has no backend information.", routeConfig.Route.RouteId);
                context.Response.StatusCode = 503;
                return Task.CompletedTask;
            }

            var dynamicState = backend.DynamicState.Value;
            if (dynamicState == null)
            {
                _logger.LogInformation("Route has no up to date information on its backend '{backend.BackendId}'. Perhaps the backend hasn't been probed yet? This can happen when a new backend is added but isn't ready to serve traffic yet.", backend.BackendId);
                context.Response.StatusCode = 503;
                return Task.CompletedTask;
            }

            if (dynamicState.HealthyEndpoints.Count == 0)
            {
                _logger.LogInformation($"No available healthy endpoints.");
                context.Response.StatusCode = 503;
                return Task.CompletedTask;
            }

            context.Features.Set(backend);
            context.Features.Set<IAvailableBackendEndpointsFeature>(new AvailableBackendEndpointsFeature() { Endpoints = dynamicState.HealthyEndpoints });

            return _next(context);
        }
    }
}

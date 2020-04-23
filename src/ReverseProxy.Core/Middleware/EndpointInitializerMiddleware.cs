// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Common;
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
                Log.NoBackendFound(_logger, routeConfig.Route.RouteId);
                context.Response.StatusCode = 503;
                return Task.CompletedTask;
            }

            var dynamicState = backend.DynamicState.Value;
            if (dynamicState == null)
            {
                Log.BackendDataNotAvailable(_logger, routeConfig.Route.RouteId, backend.BackendId);
                context.Response.StatusCode = 503;
                return Task.CompletedTask;
            }

            if (dynamicState.HealthyEndpoints.Count == 0)
            {
                Log.NoHealthyEndpoints(_logger, routeConfig.Route.RouteId, backend.BackendId);
                context.Response.StatusCode = 503;
                return Task.CompletedTask;
            }

            context.Features.Set(backend);
            context.Features.Set<IAvailableBackendEndpointsFeature>(new AvailableBackendEndpointsFeature() { Endpoints = dynamicState.HealthyEndpoints });

            return _next(context);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _noBackendFound = LoggerMessage.Define<string>(
                LogLevel.Information,
                EventIds.NoBackendFound,
                "Route `{routeId}` has no backend information.");

            private static readonly Action<ILogger, string, string, Exception> _backendDataNotAvailable = LoggerMessage.Define<string, string>(
                LogLevel.Information,
                EventIds.BackendDataNotAvailable,
                "Route `{routeId}` has no up to date information on its backend '{backendId}'. " +
                "Perhaps the backend hasn't been probed yet? " +
                "This can happen when a new backend is added but isn't ready to serve traffic yet.");

            private static readonly Action<ILogger, string, string, Exception> _noHealthyEndpoints = LoggerMessage.Define<string, string>(
                LogLevel.Information,
                EventIds.NoHealthyEndpoints,
                "Route `{routeId}` has no available healthy endpoints for Backend `{backendId}`.");

            public static void NoBackendFound(ILogger logger, string routeId)
            {
                _noBackendFound(logger, routeId, null);
            }

            public static void BackendDataNotAvailable(ILogger logger, string routeId, string backendId)
            {
                _backendDataNotAvailable(logger, routeId, backendId, null);
            }

            public static void NoHealthyEndpoints(ILogger logger, string routeId, string backendId)
            {
                _noHealthyEndpoints(logger, routeId, backendId, null);
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Service.SessionAffinity;

namespace Microsoft.ReverseProxy.Middleware
{
    /// <summary>
    /// Load balances across the available destinations.
    /// </summary>
    internal class LoadBalancingMiddleware
    {
        private readonly ILogger _logger;
        private readonly IOperationLogger<LoadBalancingMiddleware> _operationLogger;
        private readonly ILoadBalancer _loadBalancer;
        private readonly ISessionAffinityProvider _sessionAffinityProvider;
        private readonly RequestDelegate _next;

        public LoadBalancingMiddleware(
            RequestDelegate next,
            ILogger<LoadBalancingMiddleware> logger,
            IOperationLogger<LoadBalancingMiddleware> operationLogger,
            ILoadBalancer loadBalancer,
            ISessionAffinityProvider sessionAffinityProvider)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationLogger = operationLogger ?? throw new ArgumentNullException(nameof(operationLogger));
            _loadBalancer = loadBalancer ?? throw new ArgumentNullException(nameof(loadBalancer));
            _sessionAffinityProvider = sessionAffinityProvider ?? throw new ArgumentNullException(nameof(sessionAffinityProvider));
        }

        public Task Invoke(HttpContext context)
        { 
            var backend = context.Features.Get<BackendInfo>() ?? throw new InvalidOperationException("Backend unspecified.");
            var destinationsFeature = context.Features.Get<IAvailableDestinationsFeature>();
            var destinations = destinationsFeature?.Destinations
                ?? throw new InvalidOperationException("The IAvailableDestinationsFeature Destinations collection was not set.");

            var loadBalancingOptions = backend.Config.Value?.LoadBalancingOptions
                ?? new BackendConfig.BackendLoadBalancingOptions(default);
            var sessionAffinityOptions = backend.Config.Value?.SessionAffinityOptions
                ?? new BackendConfig.BackendSessionAffinityOptions(false, default, default);

            var isAffinitized = false;
            if (sessionAffinityOptions.Enabled)
            {
                var affinitizedDestinations = _sessionAffinityProvider.TryFindAffinitizedDestinations(context, destinations, sessionAffinityOptions);
                if (affinitizedDestinations.RequestKeyFound)
                {
                    isAffinitized = true;
                    if (affinitizedDestinations.Destinations.Count > 0)
                    {
                        destinations = affinitizedDestinations.Destinations;
                    }
                    else
                    {
                        Log.AffinitizedDestinationIsNotFound(_logger, affinitizedDestinations.RequestKey, backend.BackendId);
                        context.Response.StatusCode = 503;
                        return Task.CompletedTask;
                    }
                }
            }

            var destination = _operationLogger.Execute(
                "ReverseProxy.PickDestination",
                () => _loadBalancer.PickDestination(destinations, in loadBalancingOptions));

            if (destination == null)
            {
                Log.NoAvailableDestinations(_logger, backend.BackendId);
                context.Response.StatusCode = 503;
                return Task.CompletedTask;
            }

            if(sessionAffinityOptions.Enabled && !isAffinitized)
            {
                _sessionAffinityProvider.AffinitizeRequest(context, sessionAffinityOptions, destination);
            }

            destinationsFeature.Destinations = new[] { destination };

            return _next(context);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _noAvailableDestinations = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.NoAvailableDestinations,
                "No available destinations after load balancing for backend `{backendId}`.");

            private static readonly Action<ILogger, string, string, Exception> _affinitizedDestinationIsNotFound = LoggerMessage.Define<string, string>(
                LogLevel.Warning,
                EventIds.AffinitizedDestinationIsNotFound,
                "No destinations found for the affinitized request with key `{affinityKey}` on backend `{backendId}`.");

            public static void NoAvailableDestinations(ILogger logger, string backendId)
            {
                _noAvailableDestinations(logger, backendId, null);
            }

            public static void AffinitizedDestinationIsNotFound(ILogger logger, string affinityKey, string backendId)
            {
                _affinitizedDestinationIsNotFound(logger, affinityKey, backendId, null);
            }
        }
    }
}

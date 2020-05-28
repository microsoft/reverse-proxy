// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.SessionAffinity;

namespace Microsoft.ReverseProxy.Middleware
{
    /// <summary>
    /// Affinitizes request to a chosen <see cref="DestinationInfo"/>.
    /// </summary>
    internal class AffinitizeRequestMiddleware
    {
        private readonly Random _random = new Random();
        private readonly RequestDelegate _next;
        private readonly IDictionary<string, ISessionAffinityProvider> _sessionAffinityProviders;
        private readonly IOperationLogger<AffinitizeRequestMiddleware> _operationLogger;
        private readonly ILogger _logger;

        public AffinitizeRequestMiddleware(
            RequestDelegate next,
            IEnumerable<ISessionAffinityProvider> sessionAffinityProviders,
            IOperationLogger<AffinitizeRequestMiddleware> operationLogger,
            ILogger<AffinitizeRequestMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionAffinityProviders = sessionAffinityProviders.ToProviderDictionary();
            _operationLogger = operationLogger ?? throw new ArgumentNullException(nameof(operationLogger));
        }

        public Task Invoke(HttpContext context)
        {
            var backend = context.GetRequiredBackend();
            var options = backend.Config.Value?.SessionAffinityOptions ?? default;

            if (options.Enabled)
            {
                var destinationsFeature = context.GetRequiredDestinationFeature();
                var candidateDestinations = destinationsFeature.Destinations;
                if (candidateDestinations.Count == 0)
                {
                    Log.NoDestinationOnBackendToEstablishRequestAffinity(_logger, backend.BackendId);
                    context.Response.StatusCode = 503;
                    return Task.CompletedTask;
                }
                var destinations = _operationLogger.Execute("ReverseProxy.AffinitizeRequest", () => AffinitizeRequest(context, backend, options, candidateDestinations));
                destinationsFeature.Destinations = destinations;
            }

            return _next(context);
        }

        private IReadOnlyList<DestinationInfo> AffinitizeRequest(HttpContext context, BackendInfo backend, BackendConfig.BackendSessionAffinityOptions options, IReadOnlyList<DestinationInfo> destinations)
        {
            var result = destinations;
            if (result.Count > 1)
            {
                Log.MultipleDestinationsOnBackendToEstablishRequestAffinity(_logger, backend.BackendId);
                // It's assumed that all of them match to the request's affinity key.
                var singleDestination = destinations[_random.Next(destinations.Count)];
                result = new[] { singleDestination };
            }

            var currentProvider = _sessionAffinityProviders.GetRequiredServiceById(options.Mode);
            currentProvider.AffinitizeRequest(context, options, result[0]);
            return result;
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _multipleDestinationsOnBackendToEstablishRequestAffinity = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.MultipleDestinationsOnBackendToEstablishRequestAffinity,
                "The request still has multiple destinations on the backend `{backendId}` to choose from when establishing affinity, load balancing may not be properly configured. A random destination will be used.");

            private static readonly Action<ILogger, string, Exception> _noDestinationOnBackendToEstablishRequestAffinity = LoggerMessage.Define<string>(
                LogLevel.Error,
                EventIds.NoDestinationOnBackendToEstablishRequestAffinity,
                "The request doesn't have any destinations on the backend `{backendId}` to choose from when establishing affinity, load balancing may not be properly configured.");

            public static void MultipleDestinationsOnBackendToEstablishRequestAffinity(ILogger logger, string backendId)
            {
                _multipleDestinationsOnBackendToEstablishRequestAffinity(logger, backendId, null);
            }

            public static void NoDestinationOnBackendToEstablishRequestAffinity(ILogger logger, string backendId)
            {
                _noDestinationOnBackendToEstablishRequestAffinity(logger, backendId, null);
            }
        }
    }
}

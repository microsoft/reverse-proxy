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
                var destination = _operationLogger.Execute("ReverseProxy.AffinitizeRequest", () => AffinitizeRequest(context, backend, options, destinationsFeature.Destinations));
                destinationsFeature.Destinations = new[] { destination };
            }

            return _next(context);
        }

        private DestinationInfo AffinitizeRequest(HttpContext context, BackendInfo backend, BackendConfig.BackendSessionAffinityOptions options, IReadOnlyList<DestinationInfo> destinations)
        {
            var destination = destinations[0];
            if (destinations.Count > 1)
            {
                Log.AttemptToAffinitizeMultipleDestinations(_logger, backend.BackendId);
                destination = destinations[_random.Next(destinations.Count)]; // It's assumed that all of them match to the request's affinity key.
            }

            var currentProvider = _sessionAffinityProviders.GetRequiredServiceById(options.Mode);
            currentProvider.AffinitizeRequest(context, options, destination);
            return destination;
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _attemptToAffinitizeMultipleDestinations = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.AttemptToAffinitizeMultipleDestinations,
                "The request still has multiple destinations on the backend {backendId} to choose from when establishing affinity, load balancing may not be properly configured. A random destination will be used.");

            public static void AttemptToAffinitizeMultipleDestinations(ILogger logger, string backendId)
            {
                _attemptToAffinitizeMultipleDestinations(logger, backendId, null);
            }
        }
    }
}

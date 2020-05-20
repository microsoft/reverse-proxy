// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger _logger;

        public AffinitizeRequestMiddleware(RequestDelegate next, IEnumerable<ISessionAffinityProvider> sessionAffinityProviders, ILogger<AffinitizeRequestMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionAffinityProviders = sessionAffinityProviders.ToProviderDictionary();
        }

        public Task Invoke(HttpContext context)
        {
            var backend = context.GetRequiredBackend();
            var options = backend.Config.Value?.SessionAffinityOptions ?? default;

            if (options.Enabled)
            {
                var destinationsFeature = context.GetRequiredDestinationFeature();

                if (destinationsFeature.Destinations.Count > 1)
                {
                    Log.AttemptToAffinitizeMultipleDestinations(_logger, backend.BackendId);
                }

                var destinations = destinationsFeature.Destinations;
                var destination = destinations[0];
                if (destinations.Count > 1)
                {
                    Log.AttemptToAffinitizeMultipleDestinations(_logger, backend.BackendId);
                    destination = destinations[_random.Next(destinations.Count)]; // It's assumed that all of them match to the request's affinity key.
                }

                var currentProvider = _sessionAffinityProviders.GetRequiredProvider(options.Mode);
                currentProvider.AffinitizeRequest(context, options, destination);
            }

            return _next(context);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _attemptToAffinitizeMultipleDestinations = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.AttemptToAffinitizeMultipleDestinations,
                "Attempt to affinitize multiple destinations to the same request on backend `{backendId}`. The first destination will be used.");

            public static void AttemptToAffinitizeMultipleDestinations(ILogger logger, string backendId)
            {
                _attemptToAffinitizeMultipleDestinations(logger, backendId, null);
            }
        }
    }
}

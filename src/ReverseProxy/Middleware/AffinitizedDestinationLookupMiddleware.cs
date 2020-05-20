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
    /// Looks up an affinitized <see cref="DestinationInfo"/> matching the request's affinity key if any is set
    /// </summary>
    internal class AffinitizedDestinationLookupMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDictionary<string, ISessionAffinityProvider> _sessionAffinityProviders = new Dictionary<string, ISessionAffinityProvider>();
        private readonly ILogger _logger;

        public AffinitizedDestinationLookupMiddleware(RequestDelegate next, IEnumerable<ISessionAffinityProvider> sessionAffinityProviders, ILogger<AffinitizedDestinationLookupMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionAffinityProviders = sessionAffinityProviders.ToProviderDictionary();
        }

        public Task Invoke(HttpContext context)
        {
            var backend = context.GetRequiredBackend();
            var destinationsFeature = context.GetRequiredDestinationFeature();
            var destinations = destinationsFeature.Destinations;

            var options = backend.Config.Value?.SessionAffinityOptions
                ?? new BackendConfig.BackendSessionAffinityOptions(false, default, default);

            if (options.Enabled)
            {
                var currentProvider = _sessionAffinityProviders.GetRequiredProvider(options.Mode);
                if (currentProvider.TryFindAffinitizedDestinations(context, destinations, options, out var affinityResult))
                {
                    if (affinityResult.Destinations.Count > 0)
                    {
                        destinations = affinityResult.Destinations;
                    }
                    else
                    {
                        Log.AffinitizedDestinationIsNotFound(_logger, backend.BackendId);
                        context.Response.StatusCode = 503;
                        return Task.CompletedTask;
                    }
                }
            }

            return _next(context);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _affinitizedDestinationIsNotFound = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.AffinitizedDestinationIsNotFound,
                "No destinations found for the affinitized request on backend `{backendId}`.");

            public static void AffinitizedDestinationIsNotFound(ILogger logger, string backendId)
            {
                _affinitizedDestinationIsNotFound(logger, backendId, null);
            }
        }
    }
}

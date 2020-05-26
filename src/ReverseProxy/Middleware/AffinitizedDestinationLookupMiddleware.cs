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
    /// Looks up an affinitized <see cref="DestinationInfo"/> matching the request's affinity key if any is set
    /// </summary>
    internal class AffinitizedDestinationLookupMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDictionary<string, ISessionAffinityProvider> _sessionAffinityProviders;
        private readonly IOperationLogger<AffinitizedDestinationLookupMiddleware> _operationLogger;
        private readonly ILogger _logger;

        public AffinitizedDestinationLookupMiddleware(
            RequestDelegate next,
            IEnumerable<ISessionAffinityProvider> sessionAffinityProviders,
            IOperationLogger<AffinitizedDestinationLookupMiddleware> operationLogger,
            ILogger<AffinitizedDestinationLookupMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationLogger = operationLogger ?? throw new ArgumentNullException(nameof(logger));
            _sessionAffinityProviders = sessionAffinityProviders.ToProviderDictionary();
        }

        public Task Invoke(HttpContext context)
        {
            var backend = context.GetRequiredBackend();
            var destinationsFeature = context.GetRequiredDestinationFeature();
            var destinations = destinationsFeature.Destinations;

            var options = backend.Config.Value?.SessionAffinityOptions ?? default;

            if (options.Enabled)
            {
                var affinitizedDestinations = _operationLogger.Execute(
                    "ReverseProxy.FindAffinitizedDestinations",
                    () => FindAffinitizedDestinations(context, destinations, backend, options));
                if (affinitizedDestinations.DestinationsFound)
                {
                    if (affinitizedDestinations.Result.Destinations.Count > 0)
                    {
                        destinations = affinitizedDestinations.Result.Destinations;
                        destinationsFeature.Destinations = destinations;
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

        private (bool DestinationsFound, AffinityResult Result) FindAffinitizedDestinations(HttpContext context, IReadOnlyList<DestinationInfo> destinations, BackendInfo backend, BackendConfig.BackendSessionAffinityOptions options)
        {
            var currentProvider = _sessionAffinityProviders.GetRequiredServiceById(options.Mode);
            var destinationsFound = currentProvider.TryFindAffinitizedDestinations(context, destinations, backend, options, out var affinityResult);
            return (destinationsFound, affinityResult);
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

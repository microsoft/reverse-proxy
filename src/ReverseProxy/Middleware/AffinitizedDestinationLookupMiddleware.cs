// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
        private readonly IDictionary<string, IAffinityFailurePolicy> _affinityFailurePolicies;
        private readonly IOperationLogger<AffinitizedDestinationLookupMiddleware> _operationLogger;
        private readonly ILogger _logger;

        public AffinitizedDestinationLookupMiddleware(
            RequestDelegate next,
            IEnumerable<ISessionAffinityProvider> sessionAffinityProviders,
            IEnumerable<IAffinityFailurePolicy> affinityFailurePolicies,
            IOperationLogger<AffinitizedDestinationLookupMiddleware> operationLogger,
            ILogger<AffinitizedDestinationLookupMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationLogger = operationLogger ?? throw new ArgumentNullException(nameof(logger));
            _sessionAffinityProviders = sessionAffinityProviders.ToProviderDictionary();
            _affinityFailurePolicies = affinityFailurePolicies?.ToPolicyDictionary() ?? throw new ArgumentNullException(nameof(affinityFailurePolicies));
        }

        public async Task Invoke(HttpContext context)
        {
            var backend = context.GetRequiredBackend();
            var destinationsFeature = context.GetRequiredDestinationFeature();
            var destinations = destinationsFeature.Destinations;

            var options = backend.Config.Value?.SessionAffinityOptions ?? default;

            if (options.Enabled)
            {
                var affinityResult = _operationLogger.Execute(
                    "ReverseProxy.FindAffinitizedDestinations",
                    () => {
                        var currentProvider = _sessionAffinityProviders.GetRequiredServiceById(options.Mode);
                        return currentProvider.FindAffinitizedDestinations(context, destinations, backend.BackendId, options);
                    });
                switch (affinityResult.Status)
                {
                    case AffinityStatus.OK:
                        destinationsFeature.Destinations = affinityResult.Destinations;
                        break;
                    // This implementation treat them the same.
                    case AffinityStatus.AffinityDisabled:
                    case AffinityStatus.AffinityKeyNotSet:
                        // Nothing to do so just continue processing
                        break;
                    case AffinityStatus.AffinityKeyExtractionFailed:
                    case AffinityStatus.DestinationNotFound:
                        await _operationLogger.ExecuteAsync("ReverseProxy.HandleAffinityFailure", async () =>
                        {
                            var failurePolicy = _affinityFailurePolicies.GetRequiredServiceById(options.AffinityFailurePolicy);
                            if (!await failurePolicy.Handle(context, options, affinityResult.Status))
                            {
                                // Policy reported the failure is unrecoverable and took the full responsibility for its handling,
                                // so we simply stop processing.
                                Log.AffinityResolutionFailedForBackend(_logger, backend.BackendId);
                                return;
                            }
                        });
                        break;
                    default:
                        throw new NotSupportedException($"Affinity status '{affinityResult.Status}' is not supported.");
                }
            }

            await _next(context);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _affinityResolutioFailedForBackend = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.AffinityResolutionFailedForBackend,
                "Affinity resolution failed for backend `{backendId}`.");

            public static void AffinityResolutionFailedForBackend(ILogger logger, string backendId)
            {
                _affinityResolutioFailedForBackend(logger, backendId, null);
            }
        }
    }
}

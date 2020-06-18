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
            var cluster = context.GetRequiredCluster();
            var options = cluster.Config.Value?.SessionAffinityOptions ?? default;

            if (options.Enabled)
            {
                var destinationsFeature = context.GetRequiredDestinationFeature();
                var candidateDestinations = destinationsFeature.Destinations;

                if (candidateDestinations.Count == 0)
                {
                    // Only log the warning about missing destinations here, but allow the request to proceed further.
                    // The final check for selected destination is to be done at the pipeline end.
                    Log.NoDestinationOnClusterToEstablishRequestAffinity(_logger, cluster.ClusterId);
                }
                else
                {
                    var chosenDestination = candidateDestinations[0];
                    if (candidateDestinations.Count > 1)
                    {
                        Log.MultipleDestinationsOnClusterToEstablishRequestAffinity(_logger, cluster.ClusterId);
                        // It's assumed that all of them match to the request's affinity key.
                        chosenDestination = candidateDestinations[_random.Next(candidateDestinations.Count)];
                        destinationsFeature.Destinations = chosenDestination;
                    }

                    _operationLogger.Execute("ReverseProxy.AffinitizeRequest", () => AffinitizeRequest(context, options, chosenDestination));
                }
            }

            return _next(context);
        }

        private void AffinitizeRequest(HttpContext context, ClusterConfig.ClusterSessionAffinityOptions options, DestinationInfo destination)
        {
            var currentProvider = _sessionAffinityProviders.GetRequiredServiceById(options.Mode);
            currentProvider.AffinitizeRequest(context, options, destination);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _multipleDestinationsOnClusterToEstablishRequestAffinity = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.MultipleDestinationsOnClusterToEstablishRequestAffinity,
                "The request still has multiple destinations on the cluster `{clusterId}` to choose from when establishing affinity, load balancing may not be properly configured. A random destination will be used.");

            private static readonly Action<ILogger, string, Exception> _noDestinationOnClusterToEstablishRequestAffinity = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.NoDestinationOnClusterToEstablishRequestAffinity,
                "The request doesn't have any destinations on the cluster `{clusterId}` to choose from when establishing affinity, load balancing may not be properly configured.");

            public static void MultipleDestinationsOnClusterToEstablishRequestAffinity(ILogger logger, string clusterId)
            {
                _multipleDestinationsOnClusterToEstablishRequestAffinity(logger, clusterId, null);
            }

            public static void NoDestinationOnClusterToEstablishRequestAffinity(ILogger logger, string clusterId)
            {
                _noDestinationOnClusterToEstablishRequestAffinity(logger, clusterId, null);
            }
        }
    }
}

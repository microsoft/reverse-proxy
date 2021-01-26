// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.SessionAffinity;
using Microsoft.ReverseProxy.Utilities;

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

        public AffinitizeRequestMiddleware(
            RequestDelegate next,
            IEnumerable<ISessionAffinityProvider> sessionAffinityProviders,
            ILogger<AffinitizeRequestMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionAffinityProviders = sessionAffinityProviders?.ToDictionaryByUniqueId(p => p.Mode) ?? throw new ArgumentNullException(nameof(sessionAffinityProviders));
        }

        public Task Invoke(HttpContext context)
        {
            var proxyFeature = context.GetRequiredProxyFeature();
            var options = proxyFeature.ClusterConfig.Options.SessionAffinity;

            if ((options?.Enabled).GetValueOrDefault())
            {
                var candidateDestinations = proxyFeature.AvailableDestinations;

                if (candidateDestinations.Count == 0)
                {
                    var cluster = context.GetRequiredCluster();
                    // Only log the warning about missing destinations here, but allow the request to proceed further.
                    // The final check for selected destination is to be done at the pipeline end.
                    Log.NoDestinationOnClusterToEstablishRequestAffinity(_logger, cluster.ClusterId);
                }
                else
                {
                    var chosenDestination = candidateDestinations[0];
                    if (candidateDestinations.Count > 1)
                    {
                        var cluster = context.GetRequiredCluster();
                        Log.MultipleDestinationsOnClusterToEstablishRequestAffinity(_logger, cluster.ClusterId);
                        // It's assumed that all of them match to the request's affinity key.
                        chosenDestination = candidateDestinations[_random.Next(candidateDestinations.Count)];
                        proxyFeature.AvailableDestinations = chosenDestination;
                    }

                    AffinitizeRequest(context, options, chosenDestination);
                }
            }

            return _next(context);
        }

        private void AffinitizeRequest(HttpContext context, SessionAffinityOptions options, DestinationInfo destination)
        {
            var currentProvider = _sessionAffinityProviders.GetRequiredServiceById(options.Mode, SessionAffinityConstants.Modes.Cookie);
            currentProvider.AffinitizeRequest(context, options, destination);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _multipleDestinationsOnClusterToEstablishRequestAffinity = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.MultipleDestinationsOnClusterToEstablishRequestAffinity,
                "The request still has multiple destinations on the cluster '{clusterId}' to choose from when establishing affinity, load balancing may not be properly configured. A random destination will be used.");

            private static readonly Action<ILogger, string, Exception> _noDestinationOnClusterToEstablishRequestAffinity = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.NoDestinationOnClusterToEstablishRequestAffinity,
                "The request doesn't have any destinations on the cluster '{clusterId}' to choose from when establishing affinity, load balancing may not be properly configured.");

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

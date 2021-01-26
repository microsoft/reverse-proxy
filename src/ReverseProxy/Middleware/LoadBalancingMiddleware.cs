// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.LoadBalancing;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Middleware
{
    /// <summary>
    /// Load balances across the available destinations.
    /// </summary>
    internal class LoadBalancingMiddleware
    {
        private readonly ILogger _logger;
        private readonly IDictionary<string, ILoadBalancingPolicy> _loadBalancingPolicies;
        private readonly RequestDelegate _next;

        public LoadBalancingMiddleware(
            RequestDelegate next,
            ILogger<LoadBalancingMiddleware> logger,
            IEnumerable<ILoadBalancingPolicy> loadBalancingPolicies)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loadBalancingPolicies = loadBalancingPolicies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(loadBalancingPolicies));
        }

        public Task Invoke(HttpContext context)
        {
            var proxyFeature = context.GetRequiredProxyFeature();

            var destinations = proxyFeature.AvailableDestinations;
            var destinationCount = destinations.Count;

            DestinationInfo destination;

            if (destinationCount == 0)
            {
                destination = null;
            }
            else if (destinationCount == 1)
            {
                destination = destinations[0];
            }
            else
            {
                var currentPolicy = _loadBalancingPolicies.GetRequiredServiceById(proxyFeature.ClusterConfig.Options.LoadBalancingPolicy, LoadBalancingPolicies.PowerOfTwoChoices);
                destination = currentPolicy.PickDestination(context, destinations);
            }

            if (destination == null)
            {
                var cluster = context.GetRequiredCluster();
                Log.NoAvailableDestinations(_logger, cluster.ClusterId);
                context.Response.StatusCode = 503;
                return Task.CompletedTask;
            }

            proxyFeature.AvailableDestinations = destination;

            return _next(context);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _noAvailableDestinations = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.NoAvailableDestinations,
                "No available destinations after load balancing for cluster '{clusterId}'.");

            public static void NoAvailableDestinations(ILogger logger, string clusterId)
            {
                _noAvailableDestinations(logger, clusterId, null);
            }
        }
    }
}

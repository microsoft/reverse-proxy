// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy;

namespace Microsoft.ReverseProxy.Middleware
{
    /// <summary>
    /// Load balances across the available destinations.
    /// </summary>
    internal class LoadBalancingMiddleware
    {
        private readonly ILogger _logger;
        private readonly ILoadBalancer _loadBalancer;
        private readonly RequestDelegate _next;

        public LoadBalancingMiddleware(
            RequestDelegate next,
            ILogger<LoadBalancingMiddleware> logger,
            ILoadBalancer loadBalancer)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loadBalancer = loadBalancer ?? throw new ArgumentNullException(nameof(loadBalancer));
        }

        public Task Invoke(HttpContext context)
        { 
            var proxyFeature = context.GetRequiredProxyFeature();
            var destinations = proxyFeature.AvailableDestinations;

            var loadBalancingOptions = proxyFeature.ClusterConfig.LoadBalancingOptions;

            var destination = _loadBalancer.PickDestination(destinations, in loadBalancingOptions);

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
                "No available destinations after load balancing for cluster `{clusterId}`.");

            public static void NoAvailableDestinations(ILogger logger, string clusterId)
            {
                _noAvailableDestinations(logger, clusterId, null);
            }
        }
    }
}

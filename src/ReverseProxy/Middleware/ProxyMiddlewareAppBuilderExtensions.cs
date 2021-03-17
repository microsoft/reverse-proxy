// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.Middleware;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Extensions for adding proxy middleware to the pipeline.
    /// </summary>
    public static class ProxyMiddlewareAppBuilderExtensions
    {
        /// <summary>
        /// Load balances across the available endpoints.
        /// </summary>
        public static IApplicationBuilder UseProxyLoadBalancing(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<LoadBalancingMiddleware>();
        }

        /// <summary>
        /// Checks if a request has an established affinity relationship and if the associated destination is available.
        /// This should be placed before load balancing and other destination selection components.
        /// Requests without an affinity relationship will be processed normally and have the affinity relationship
        /// established by a later component.
        /// </summary>
        public static IApplicationBuilder UseAffinitizedDestinationLookup(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AffinitizedDestinationLookupMiddleware>();
        }

        /// <summary>
        /// Passively checks destinations health by watching for successes and failures in client request proxying.
        /// </summary>
        public static IApplicationBuilder UsePassiveHealthChecks(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PassiveHealthCheckMiddleware>();
        }
    }
}

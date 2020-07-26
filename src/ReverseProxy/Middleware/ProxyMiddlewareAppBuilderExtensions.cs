// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Middleware;

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
        /// established by a later component. See <see cref="UseRequestAffinitizer"/>.
        /// </summary>
        public static IApplicationBuilder UseAffinitizedDestinationLookup(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AffinitizedDestinationLookupMiddleware>();
        }

        /// <summary>
        /// Establishes the affinity relationship to the selected destination.
        /// If there are multiple affinitized destinations found for the request, it randomly picks one of them.
        /// This should be placed after load balancing and other destination selection processes.
        /// </summary>
        public static IApplicationBuilder UseRequestAffinitizer(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AffinitizeRequestMiddleware>();
        }
    }
}

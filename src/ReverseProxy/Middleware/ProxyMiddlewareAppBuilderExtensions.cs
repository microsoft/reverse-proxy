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
        /// Looks up one or multiple destinations affinitized to the request by an affinity key.
        /// It only find affinitized destinations, but do not actually routes request to any of them.
        /// Instead destinations are passed further to pipeline for load balancing or other processing steps.
        /// </summary>
        public static IApplicationBuilder UseAffinitizedDestinationLookup(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AffinitizedDestinationLookupMiddleware>();
        }

        /// <summary>
        /// Routes the request to an affinitized destination looked up on previous steps.
        /// If there are multiple affinitized destinations found for the request, it randomly picks one of them.
        /// </summary>
        public static IApplicationBuilder UseRequestAffinity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AffinitizeRequestMiddleware>();
        }
    }
}

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
        /// Load balances across the available endpoints and maintains session affinity.
        /// </summary>
        public static IApplicationBuilder UseProxyLoadBalancingWithSessionAffinity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AffinitizedDestinationLookupMiddleware>()
                .UseMiddleware<LoadBalancingMiddleware>()
                .UseMiddleware<AffinitizeRequestMiddleware>();
        }
    }
}

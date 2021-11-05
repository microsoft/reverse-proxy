// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.LoadBalancing;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extensions for adding proxy middleware to the pipeline.
/// </summary>
public static class AppBuilderLoadBalancingExtensions
{
    /// <summary>
    /// Load balances across the available endpoints.
    /// </summary>
    public static IReverseProxyApplicationBuilder UseLoadBalancing(this IReverseProxyApplicationBuilder builder)
    {
        builder.UseMiddleware<LoadBalancingMiddleware>();
        return builder;
    }
}

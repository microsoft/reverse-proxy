// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET6_0_OR_GREATER
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Delegation;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extensions for adding delegation middleware to the pipeline.
/// </summary>
public static class AppBuilderDelegationExtensions
{
    /// <summary>
    /// Adds middleware to check if the selected destination should use Http.sys delegation. If so the request is delegated to the destination queue.
    /// This should be placed after load balancing and passive health checks.
    /// </summary>
    /// <remarks>
    /// This middleware only works with the ASP.NET Core Http.sys server implementation.
    /// A <see cref="IHttpSysDelegationRuleManager"/> must be registered with DI. This can be done by calling <see cref="ReverseProxyServiceCollectionExtensions.AddHttpSysDelegation(IReverseProxyBuilder)"/>.
    /// </remarks>
    public static IReverseProxyApplicationBuilder UseHttpSysDelegation(this IReverseProxyApplicationBuilder builder)
    {
        builder.UseMiddleware<HttpSysDelegationMiddleware>();
        return builder;
    }
}
#endif

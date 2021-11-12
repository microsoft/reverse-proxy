// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Extension methods for fetching proxy configuration from the current HttpContext.
/// </summary>
public static class HttpContextFeaturesExtensions
{
    /// <summary>
    /// Retrieves the <see cref="RouteModel"/> instance associated with the current request.
    /// </summary>
    public static RouteModel GetRouteModel(this HttpContext context)
    {
        var proxyFeature = context.GetReverseProxyFeature();

        var route = proxyFeature.Route
            ?? throw new InvalidOperationException($"The {typeof(IReverseProxyFeature).FullName} is missing the {typeof(RouteModel).FullName}.");

        return route;
    }

    /// <summary>
    /// Retrieves the <see cref="IReverseProxyFeature"/> instance associated with the current request.
    /// </summary>
    public static IReverseProxyFeature GetReverseProxyFeature(this HttpContext context)
    {
        return context.Features.Get<IReverseProxyFeature>() ?? throw new InvalidOperationException($"{typeof(IReverseProxyFeature).FullName} is missing.");
    }

    /// <summary>
    /// Retrieves the <see cref="IForwarderErrorFeature"/> instance associated with the current request, if any.
    /// </summary>
    public static IForwarderErrorFeature? GetForwarderErrorFeature(this HttpContext context)
    {
        return context.Features.Get<IForwarderErrorFeature>();
    }
}

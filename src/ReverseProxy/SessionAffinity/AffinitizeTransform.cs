// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Transforms;

namespace Yarp.ReverseProxy.SessionAffinity;

/// <summary>
/// Affinitizes the request to a chosen <see cref="DestinationState"/>.
/// </summary>
internal sealed class AffinitizeTransform : ResponseTransform
{
    private readonly ISessionAffinityPolicy _sessionAffinityPolicy;

    public AffinitizeTransform(ISessionAffinityPolicy sessionAffinityPolicy)
    {
        _sessionAffinityPolicy = sessionAffinityPolicy ?? throw new ArgumentNullException(nameof(sessionAffinityPolicy));
    }

    public override ValueTask ApplyAsync(ResponseTransformContext context)
    {
        var proxyFeature = context.HttpContext.GetReverseProxyFeature();
        var options = proxyFeature.Cluster.Config.SessionAffinity;
        // The transform should only be added to routes that have affinity enabled.
        // However, the cluster can be re-assigned dynamically.
        if (options is null || !options.Enabled.GetValueOrDefault())
        {
            return default;
        }
        var selectedDestination = proxyFeature.ProxiedDestination!;
        _sessionAffinityPolicy.AffinitizeResponse(context.HttpContext, proxyFeature.Route.Cluster!, options!, selectedDestination);
        return default;
    }
}

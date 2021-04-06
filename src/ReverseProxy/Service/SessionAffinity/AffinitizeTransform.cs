// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Yarp.ReverseProxy.Service.SessionAffinity
{
    /// <summary>
    /// Affinitizes the request to a chosen <see cref="DestinationInfo"/>.
    /// </summary>
    internal class AffinitizeTransform : ResponseTransform
    {
        private readonly ISessionAffinityProvider _sessionAffinityProvider;

        public AffinitizeTransform(ISessionAffinityProvider sessionAffinityProvider)
        {
            _sessionAffinityProvider = sessionAffinityProvider ?? throw new ArgumentNullException(nameof(sessionAffinityProvider));
        }

        public override ValueTask ApplyAsync(ResponseTransformContext context)
        {
            var proxyFeature = context.HttpContext.GetRequiredProxyFeature();
            var config = proxyFeature.ClusterSnapshot;
            // The transform should only be added to routes that have affinity enabled.
            Debug.Assert(config.Options.SessionAffinity?.Enabled ?? false, "Session affinity is not enabled");
            var selectedDestination = proxyFeature.ProxiedDestination;
            _sessionAffinityProvider.AffinitizeRequest(context.HttpContext, proxyFeature.ClusterSnapshot, selectedDestination);
            return default;
        }
    }
}

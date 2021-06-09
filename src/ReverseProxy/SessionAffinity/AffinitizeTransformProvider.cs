// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Transforms.Builder;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.SessionAffinity
{
    internal sealed class AffinitizeTransformProvider : ITransformProvider
    {
        private readonly IDictionary<string, ISessionAffinityProvider> _sessionAffinityProviders;

        public AffinitizeTransformProvider(IEnumerable<ISessionAffinityProvider> sessionAffinityProviders)
        {
            _sessionAffinityProviders = sessionAffinityProviders?.ToDictionaryByUniqueId(p => p.Name)
                ?? throw new ArgumentNullException(nameof(sessionAffinityProviders));
        }

        public void ValidateRoute(TransformRouteValidationContext context)
        {
        }

        public void ValidateCluster(TransformClusterValidationContext context)
        {
            // Other affinity validation logic is covered by ConfigValidator.ValidateSessionAffinity.
            if (!(context.Cluster.SessionAffinity?.Enabled ?? false))
            {
                return;
            }

            var provider = context.Cluster.SessionAffinity.Provider;
            if (string.IsNullOrEmpty(provider))
            {
                // The default.
                provider = SessionAffinityConstants.Providers.Cookie;
            }

            if (!_sessionAffinityProviders.ContainsKey(provider))
            {
                context.Errors.Add(new ArgumentException($"No matching {nameof(ISessionAffinityProvider)} found for the session affinity provider '{provider}' set on the cluster '{context.Cluster.ClusterId}'."));
            }
        }

        public void Apply(TransformBuilderContext context)
        {
            var options = context.Cluster?.SessionAffinity;

            if (options != null && options.Enabled.GetValueOrDefault())
            {
                var provider = _sessionAffinityProviders.GetRequiredServiceById(options.Provider, SessionAffinityConstants.Providers.Cookie);
                context.ResponseTransforms.Add(new AffinitizeTransform(provider));
            }
        }
    }
}

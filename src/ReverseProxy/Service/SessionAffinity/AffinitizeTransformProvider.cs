// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.Abstractions.Config;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class AffinitizeTransformProvider : ITransformProvider
    {
        private readonly IDictionary<string, ISessionAffinityProvider> _sessionAffinityProviders;

        public AffinitizeTransformProvider(IEnumerable<ISessionAffinityProvider> sessionAffinityProviders)
        {
            _sessionAffinityProviders = sessionAffinityProviders?.ToDictionaryByUniqueId(p => p.Mode)
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

            var affinityMode = context.Cluster.SessionAffinity.Mode;
            if (string.IsNullOrEmpty(affinityMode))
            {
                // The default.
                affinityMode = SessionAffinityConstants.Modes.Cookie;
            }

            if (!_sessionAffinityProviders.ContainsKey(affinityMode))
            {
                context.Errors.Add(new ArgumentException($"No matching {nameof(ISessionAffinityProvider)} found for the session affinity mode '{affinityMode}' set on the cluster '{context.Cluster.Id}'."));
            }
        }

        public void Apply(TransformBuilderContext context)
        {
            var options = context.Cluster?.SessionAffinity;

            if ((options?.Enabled).GetValueOrDefault())
            {
                var provider = _sessionAffinityProviders.GetRequiredServiceById(options.Mode, SessionAffinityConstants.Modes.Cookie);
                context.ResponseTransforms.Add(new AffinitizeTransform(provider));
            }
        }
    }
}

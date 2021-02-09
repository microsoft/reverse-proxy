// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions;
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
            _sessionAffinityProviders = sessionAffinityProviders?.ToDictionaryByUniqueId(p => p.Mode) ?? throw new ArgumentNullException(nameof(sessionAffinityProviders));
        }

        public void Validate(TransformValidationContext context)
        {
            throw new NotImplementedException();
        }

        public void Apply(TransformBuilderContext context)
        {
            var cluster = context.Cluster;
            var options = cluster.SessionAffinity;

            if ((options?.Enabled).GetValueOrDefault())
            {
                var provider = _sessionAffinityProviders.GetRequiredServiceById(options.Mode, SessionAffinityConstants.Modes.Cookie);
                context.ResponseTransforms.Add(new AffinitizeTransform(provider));
            }
        }
    }
}

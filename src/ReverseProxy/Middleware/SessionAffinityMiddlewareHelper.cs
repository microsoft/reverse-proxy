// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Service.SessionAffinity;

namespace Microsoft.ReverseProxy.Middleware
{
    internal static class SessionAffinityMiddlewareHelper
    {
        public static IDictionary<string, ISessionAffinityProvider> ToProviderDictionary(this IEnumerable<ISessionAffinityProvider> sessionAffinityProviders)
        {
            if (sessionAffinityProviders == null)
            {
                throw new ArgumentNullException(nameof(sessionAffinityProviders));
            }

            var result = new Dictionary<string, ISessionAffinityProvider>(StringComparer.OrdinalIgnoreCase);

            foreach (var provider in sessionAffinityProviders)
            {
                if (!result.TryAdd(provider.Mode, provider))
                {
                    throw new ArgumentException(nameof(sessionAffinityProviders), $"More than one {nameof(ISessionAffinityProvider)} found with the same Mode value.");
                }
            }

            return result;
        }

        public static ISessionAffinityProvider GetRequiredProvider(this IDictionary<string, ISessionAffinityProvider> sessionAffinityProviders, string mode)
        {
            if (!sessionAffinityProviders.TryGetValue(mode, out var currentProvider))
            {
                throw new ArgumentException(nameof(mode), $"No {nameof(ISessionAffinityProvider)} was found for the mode {mode}.");
            }
            return currentProvider;
        }
    }
}

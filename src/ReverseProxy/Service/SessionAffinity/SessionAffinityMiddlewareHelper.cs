// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Service.SessionAffinity;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal static class SessionAffinityMiddlewareHelper
    {
        public static IDictionary<string, T> ToDictionaryById<T>(this IEnumerable<T> services, Func<T, string> idSelector)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var result = new Dictionary<string, T>(StringComparer.Ordinal);

            foreach (var service in services)
            {
                if (!result.TryAdd(idSelector(service), service))
                {
                    throw new ArgumentException(nameof(services), $"More than one {nameof(T)} found with the same identifier.");
                }
            }

            return result;
        }

        public static IDictionary<string, ISessionAffinityProvider> ToProviderDictionary(this IEnumerable<ISessionAffinityProvider> sessionAffinityProviders)
        {
            return ToDictionaryById(sessionAffinityProviders, p => p.Mode);
        }

        public static IDictionary<string, IAffinityFailurePolicy> ToPolicyDictionary(this IEnumerable<IAffinityFailurePolicy> affinityFailurePolicies)
        {
            return ToDictionaryById(affinityFailurePolicies, p => p.Name);
        }

        public static T GetRequiredServiceById<T>(this IDictionary<string, T> services, string id)
        {
            if (!services.TryGetValue(id, out var result))
            {
                throw new ArgumentException(nameof(id), $"No {nameof(T)} was found for the id {id}.");
            }
            return result;
        }
    }
}

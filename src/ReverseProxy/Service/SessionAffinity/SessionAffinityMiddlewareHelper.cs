// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Utilities;
using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal static class SessionAffinityMiddlewareHelper
    {
        public static IDictionary<string, ISessionAffinityProvider> ToProviderDictionary(this IEnumerable<ISessionAffinityProvider> sessionAffinityProviders)
        {
            return sessionAffinityProviders.ToDictionaryByUniqueId(p => p.Mode);
        }

        public static IDictionary<string, IAffinityFailurePolicy> ToPolicyDictionary(this IEnumerable<IAffinityFailurePolicy> affinityFailurePolicies)
        {
            return affinityFailurePolicies.ToDictionaryByUniqueId(p => p.Name);
        }
    }
}

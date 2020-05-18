// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class SessionAffinityProvider : ISessionAffinityProvider
    {
        private const string CookieName = "ms-rev-proxy-sess-key";

        //TBD. Add logging.

        public void AffinitizeRequest(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, DestinationInfo destination)
        {
            if (!options.Enabled)
            {
                return;
            }

            var affinityKey = destination.DestinationId; // Currently, we always use ID as the affinity key

            context.Features.Set<ISessionAffinityFeature>(new SessionAffinityFeature { DestinationKey = affinityKey, Mode = options.Mode, CustomHeaderName = options.CustomHeaderName });
        }

        public AffinitizedDestinationCollection TryFindAffinitizedDestinations(HttpContext context, IReadOnlyList<DestinationInfo> destinations, BackendConfig.BackendSessionAffinityOptions options)
        {
            if (!options.Enabled || destinations.Count == 0)
            {
                return default;
            }

            var requestAffinityKey = GetAffinityKey(context, options);

            // TBD. Support different failure modes
            if (requestAffinityKey == null)
            {
                return default;
            }

            context.Features.Set<ISessionAffinityFeature>(new SessionAffinityFeature { DestinationKey = requestAffinityKey, Mode = options.Mode, CustomHeaderName = options.CustomHeaderName });

            // It's allowed to affinitize a request to a pool of destinations so as to enable load-balancing among them
            var matchingDestinations = new List<DestinationInfo>();
            for(var i = 0; i < destinations.Count; i++)
            {
                if (destinations[i].DestinationId == requestAffinityKey)
                {
                    matchingDestinations.Add(destinations[i]);
                }
            }

            return new AffinitizedDestinationCollection(matchingDestinations, requestAffinityKey);
        }

        public void SetAffinityKeyOnDownstreamResponse(HttpContext context)
        {
            var affinityFeature = context.Features.Get<ISessionAffinityFeature>();

            if (affinityFeature == null)
            {
                return;
            }

            // TBD. The affinity key must be encrypted.
            switch (affinityFeature.Mode)
            {
                case SessionAffinityMode.Cookie:
                    context.Response.Cookies.Append(CookieName, affinityFeature.DestinationKey);
                    return;
                case SessionAffinityMode.CustomHeader:
                    context.Response.Headers.Add(affinityFeature.CustomHeaderName, new StringValues(affinityFeature.DestinationKey));
                    return;
            }
        }

        private string GetAffinityKey(HttpContext context, BackendConfig.BackendSessionAffinityOptions options)
        {
            switch (options.Mode)
            {
                case SessionAffinityMode.Cookie:
                    return context.Request.Cookies.TryGetValue(CookieName, out var keyInCookie) ? keyInCookie : null;
                case SessionAffinityMode.CustomHeader:
                    var keyHeaderValues = context.Request.Headers[options.CustomHeaderName];
                    return !StringValues.IsNullOrEmpty(keyHeaderValues) ? keyHeaderValues[0] : null; // We always take the first value of a custom header storing an affinity key
                default:
                    throw new ArgumentOutOfRangeException(nameof(options), $"Unsupported value {options.Mode} of {nameof(SessionAffinityMode)}.");
            }
        }
    }
}

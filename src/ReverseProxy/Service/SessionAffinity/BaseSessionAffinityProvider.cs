// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal abstract class BaseSessionAffinityProvider<T> : ISessionAffinityProvider
    {
        protected static readonly object AffinityKeyId = new object();
        protected readonly IDataProtector DataProtector;
        protected readonly IDictionary<string, IMissingDestinationHandler> MissingDestinationHandlers;

        protected BaseSessionAffinityProvider(IDataProtectionProvider dataProtectionProvider, IEnumerable<IMissingDestinationHandler> missingDestinationHandlers)
        {
            DataProtector = dataProtectionProvider?.CreateProtector(GetType().FullName) ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
            MissingDestinationHandlers = missingDestinationHandlers?.ToHandlerDictionary() ?? throw new ArgumentNullException(nameof(missingDestinationHandlers));
        }

        public abstract string Mode { get; }

        public virtual void AffinitizeRequest(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, DestinationInfo destination)
        {
            if (!options.Enabled)
            {
                return;
            }

            if (!context.Items.TryGetValue(AffinityKeyId, out var affinityKey)) // If affinity key is already set on request, we assume that passed destination always matches to that key
            {
                affinityKey = GetDestinationAffinityKey(destination);
            }

            SetAffinityKey(context, options, (T)affinityKey);
        }

        public virtual bool TryFindAffinitizedDestinations(HttpContext context, IReadOnlyList<DestinationInfo> destinations, BackendConfig.BackendSessionAffinityOptions options, out AffinityResult affinityResult)
        {
            if (!options.Enabled || destinations.Count == 0)
            {
                affinityResult = default;
                return false;
            }

            var requestAffinityKey = GetRequestAffinityKey(context, options);

            if (requestAffinityKey == null)
            {
                affinityResult = default;
                return false;
            }

            context.Items.Add(AffinityKeyId, requestAffinityKey);

            // It's allowed to affinitize a request to a pool of destinations so as to enable load-balancing among them
            var matchingDestinations = new List<DestinationInfo>();
            for (var i = 0; i < destinations.Count; i++)
            {
                if (requestAffinityKey.Equals(GetDestinationAffinityKey(destinations[i])))
                {
                    matchingDestinations.Add(destinations[i]);
                }
            }

            if (matchingDestinations.Count == 0)
            {
                var failureHandler = MissingDestinationHandlers[options.MissingDestinationHandler];
                var newAffinitizedDestinations = failureHandler.Handle(context, options, requestAffinityKey, destinations);
                affinityResult = new AffinityResult(newAffinitizedDestinations);
            }
            else
            {
                affinityResult = new AffinityResult(matchingDestinations);
            }

            return true;
        }

        protected virtual string GetSettingValue(string key, BackendConfig.BackendSessionAffinityOptions options)
        {
            if (options.Settings == null || !options.Settings.TryGetValue(key, out var value))
            {
                throw new ArgumentException(nameof(options), $"{nameof(CookieSessionAffinityProvider)} couldn't find the required parameter {key} in session affinity settings.");
            }

            return value;
        }

        protected abstract T GetDestinationAffinityKey(DestinationInfo destination);

        protected abstract T GetRequestAffinityKey(HttpContext context, BackendConfig.BackendSessionAffinityOptions options);

        protected abstract void SetAffinityKey(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, T unencryptedKey);
    }
}

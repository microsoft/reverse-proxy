// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal abstract class BaseSessionAffinityProvider<T> : ISessionAffinityProvider
    {
        protected static readonly object AffinityKeyId = new object();
        protected readonly IDataProtector DataProtector;
        protected readonly IDictionary<string, IMissingDestinationHandler> MissingDestinationHandlers;
        protected readonly ILogger Logger;

        protected BaseSessionAffinityProvider(IDataProtectionProvider dataProtectionProvider, IEnumerable<IMissingDestinationHandler> missingDestinationHandlers, ILogger logger)
        {
            DataProtector = dataProtectionProvider?.CreateProtector(GetType().FullName) ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
            MissingDestinationHandlers = missingDestinationHandlers?.ToHandlerDictionary() ?? throw new ArgumentNullException(nameof(missingDestinationHandlers));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public abstract string Mode { get; }

        public virtual void AffinitizeRequest(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, DestinationInfo destination)
        {
            if (!options.Enabled)
            {
                Log.RequestAffinityToDestinationCannotBeEstablishedBecauseAffinitizationDisabled(Logger, destination.DestinationId);
                return;
            }

            if (!context.Items.TryGetValue(AffinityKeyId, out var affinityKey)) // If affinity key is already set on request, we assume that passed destination always matches to that key
            {
                affinityKey = GetDestinationAffinityKey(destination);
            }

            SetAffinityKey(context, options, (T)affinityKey);
        }

        public virtual bool TryFindAffinitizedDestinations(HttpContext context, IReadOnlyList<DestinationInfo> destinations, BackendInfo backend, BackendConfig.BackendSessionAffinityOptions options, out AffinityResult affinityResult)
        {
            if (!options.Enabled)
            {
                affinityResult = default;
                return false;
            }

            if (destinations.Count == 0)
            {
                Log.AffinityCannotBeEstablishedBecauseNoDestinationsFound(Logger, backend.BackendId);
                affinityResult = default;
                return false;
            }

            var requestAffinityKey = GetRequestAffinityKey(context, options);

            if (requestAffinityKey == null)
            {
                affinityResult = default;
                return false;
            }

            if (!context.Items.TryAdd(AffinityKeyId, requestAffinityKey))
            {
                Log.RequestAffinityKeyAlreadyPresentInContext(Logger, backend.BackendId);
                throw new InvalidOperationException("Request affinitization failed.");
            }

            var matchingDestinations = new DestinationInfo[1];
            for (var i = 0; i < destinations.Count; i++)
            {
                if (requestAffinityKey.Equals(GetDestinationAffinityKey(destinations[i])))
                {
                    matchingDestinations[0] = destinations[i]; // It's allowed to affinitize a request to a pool of destinations so as to enable load-balancing among them.
                    break;                                     // However, we currently stop after the first match found to avoid performance degradation.
                }
            }

            if (matchingDestinations.Length == 0)
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

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _affinityCannotBeEstablishedBecauseNoDestinationsFound = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnBackend,
                "The request affinity cannot be established because no destinations are found on backend `{backendId}`.");

            private static readonly Action<ILogger, string, Exception> _requestAffinityToDestinationCannotBeEstablishedBecauseAffinitizationDisabled = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.RequestAffinityToDestinationCannotBeEstablishedBecauseAffinitizationDisabled,
                "The request affinity to destination `{destinationId}` cannot be established because affinitization is disabled for the backend.");

            private static readonly Action<ILogger, string, Exception> _requestAffinityKeyAlreadyPresentInContext = LoggerMessage.Define<string>(
                LogLevel.Error,
                EventIds.RequestAffinityKeyAlreadyPresentInContext,
                "The request affinity key is already present in HttpContext. Affinization failed for backend `{backendId}`.");

            public static void AffinityCannotBeEstablishedBecauseNoDestinationsFound(ILogger logger, string backendId)
            {
                _affinityCannotBeEstablishedBecauseNoDestinationsFound(logger, backendId, null);
            }

            public static void RequestAffinityToDestinationCannotBeEstablishedBecauseAffinitizationDisabled(ILogger logger, string destinationId)
            {
                _requestAffinityToDestinationCannotBeEstablishedBecauseAffinitizationDisabled(logger, destinationId, null);
            }

            public static void RequestAffinityKeyAlreadyPresentInContext(ILogger logger, string backendId)
            {
                _requestAffinityKeyAlreadyPresentInContext(logger, backendId, null);
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.SessionAffinity
{
    internal abstract class BaseSessionAffinityProvider<T> : ISessionAffinityProvider
    {
        private readonly IDataProtector _dataProtector;
        protected static readonly object AffinityKeyId = new object();
        protected readonly ILogger Logger;

        protected BaseSessionAffinityProvider(IDataProtectionProvider dataProtectionProvider, ILogger logger)
        {
            _dataProtector = dataProtectionProvider?.CreateProtector(GetType().FullName!) ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public abstract string Mode { get; }

        public virtual void AffinitizeRequest(HttpContext context, SessionAffinityConfig config, DestinationState destination)
        {
            if (!config.Enabled.GetValueOrDefault())
            {
                throw new InvalidOperationException($"Session affinity is disabled for cluster.");
            }

            // Affinity key is set on the response only if it's a new affinity.
            if (!context.Items.ContainsKey(AffinityKeyId))
            {
                var affinityKey = GetDestinationAffinityKey(destination);
                SetAffinityKey(context, config, affinityKey);
            }
        }

        public virtual AffinityResult FindAffinitizedDestinations(HttpContext context, IReadOnlyList<DestinationState> destinations, string clusterId, SessionAffinityConfig config)
        {
            if (!config.Enabled.GetValueOrDefault())
            {
                throw new InvalidOperationException($"Session affinity is disabled for cluster {clusterId}.");
            }

            var requestAffinityKey = GetRequestAffinityKey(context, config);

            if (requestAffinityKey.Key == null)
            {
                return new AffinityResult(null, requestAffinityKey.ExtractedSuccessfully ? AffinityStatus.AffinityKeyNotSet : AffinityStatus.AffinityKeyExtractionFailed);
            }

            IReadOnlyList<DestinationState>? matchingDestinations = null;
            if (destinations.Count > 0)
            {
                for (var i = 0; i < destinations.Count; i++)
                {
                    // TODO: Add fast destination lookup by ID
                    if (requestAffinityKey.Key.Equals(GetDestinationAffinityKey(destinations[i])))
                    {
                        // It's allowed to affinitize a request to a pool of destinations so as to enable load-balancing among them.
                        // However, we currently stop after the first match found to avoid performance degradation.
                        matchingDestinations = destinations[i];
                        break;
                    }
                }

                if (matchingDestinations == null)
                {
                    Log.DestinationMatchingToAffinityKeyNotFound(Logger, clusterId);
                }
            }
            else
            {
                Log.AffinityCannotBeEstablishedBecauseNoDestinationsFound(Logger, clusterId);
            }

            // Empty destination list passed to this method is handled the same way as if no matching destinations are found.
            if (matchingDestinations == null)
            {
                return new AffinityResult(null, AffinityStatus.DestinationNotFound);
            }

            context.Items[AffinityKeyId] = requestAffinityKey;
            return new AffinityResult(matchingDestinations, AffinityStatus.OK);
        }

        protected abstract T GetDestinationAffinityKey(DestinationState destination);

        protected abstract (T? Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, SessionAffinityConfig config);

        protected abstract void SetAffinityKey(HttpContext context, SessionAffinityConfig config, T unencryptedKey);

        protected string Protect(string unencryptedKey)
        {
            if (string.IsNullOrEmpty(unencryptedKey))
            {
                return unencryptedKey;
            }

            var userData = Encoding.UTF8.GetBytes(unencryptedKey);

            var protectedData = _dataProtector.Protect(userData);
            return Convert.ToBase64String(protectedData).TrimEnd('=');
        }

        protected (string? Key, bool ExtractedSuccessfully) Unprotect(string? encryptedRequestKey)
        {
            if (string.IsNullOrEmpty(encryptedRequestKey))
            {
                return (Key: null, ExtractedSuccessfully: true);
            }

            try
            {
                var keyBytes = Convert.FromBase64String(Pad(encryptedRequestKey));

                var decryptedKeyBytes = _dataProtector.Unprotect(keyBytes);
                if (decryptedKeyBytes == null)
                {
                    Log.RequestAffinityKeyDecryptionFailed(Logger, null);
                    return (Key: null, ExtractedSuccessfully: false);
                }

                return (Key: Encoding.UTF8.GetString(decryptedKeyBytes), ExtractedSuccessfully: true);
            }
            catch (Exception ex)
            {
                Log.RequestAffinityKeyDecryptionFailed(Logger, ex);
                return (Key: null, ExtractedSuccessfully: false);
            }
        }

        private static string Pad(string text)
        {
            var padding = 3 - ((text.Length + 3) % 4);
            if (padding == 0)
            {
                return text;
            }
            return text + new string('=', padding);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception?> _affinityCannotBeEstablishedBecauseNoDestinationsFound = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster,
                "The request affinity cannot be established because no destinations are found on cluster `{clusterId}`.");

            private static readonly Action<ILogger, Exception?> _requestAffinityKeyDecryptionFailed = LoggerMessage.Define(
                LogLevel.Error,
                EventIds.RequestAffinityKeyDecryptionFailed,
                "The request affinity key decryption failed.");

            private static readonly Action<ILogger, string, Exception?> _destinationMatchingToAffinityKeyNotFound = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.DestinationMatchingToAffinityKeyNotFound,
                "Destination matching to the request affinity key is not found on cluster `{backnedId}`. Configured failure policy will be applied.");

            public static void AffinityCannotBeEstablishedBecauseNoDestinationsFound(ILogger logger, string clusterId)
            {
                _affinityCannotBeEstablishedBecauseNoDestinationsFound(logger, clusterId, null);
            }

            public static void RequestAffinityKeyDecryptionFailed(ILogger logger, Exception? ex)
            {
                _requestAffinityKeyDecryptionFailed(logger, ex);
            }

            public static void DestinationMatchingToAffinityKeyNotFound(ILogger logger, string clusterId)
            {
                _destinationMatchingToAffinityKeyNotFound(logger, clusterId, null);
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal abstract class BaseSessionAffinityProvider<T> : ISessionAffinityProvider
    {
        private readonly IDataProtector _dataProtector;
        protected static readonly object AffinityKeyId = new object();
        protected readonly ILogger Logger;

        protected BaseSessionAffinityProvider(IDataProtectionProvider dataProtectionProvider, ILogger logger)
        {
            _dataProtector = dataProtectionProvider?.CreateProtector(GetType().FullName) ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public abstract string Mode { get; }

        public virtual void AffinitizeRequest(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options, DestinationInfo destination)
        {
            if (!options.Enabled)
            {
                Log.RequestAffinityToDestinationCannotBeEstablishedBecauseAffinitizationDisabled(Logger, destination.DestinationId);
                return;
            }

            // If affinity key is already set on request, we assume that passed destination always matches to that key
            if (!context.Items.TryGetValue(AffinityKeyId, out var affinityKey))
            {
                affinityKey = GetDestinationAffinityKey(destination);
            }

            SetAffinityKey(context, options, (T)affinityKey);
        }

        public virtual AffinityResult FindAffinitizedDestinations(HttpContext context, IReadOnlyList<DestinationInfo> destinations, string backendId, in BackendConfig.BackendSessionAffinityOptions options)
        {
            if (!options.Enabled)
            {
                // This case is handled separately to improve the type autonomy and the pipeline extensibility
                return new AffinityResult(null, AffinityStatus.AffinityDisabled);
            }

            var requestAffinityKey = GetRequestAffinityKey(context, options);

            if (requestAffinityKey.Key == null)
            {
                return new AffinityResult(null, requestAffinityKey.ExtractedSuccessfully ? AffinityStatus.AffinityKeyNotSet : AffinityStatus.AffinityKeyExtractionFailed);
            }

            var matchingDestinations = new DestinationInfo[1];
            if (destinations.Count > 0)
            {
                context.Items[AffinityKeyId] = requestAffinityKey;

                for (var i = 0; i < destinations.Count; i++)
                {
                    if (requestAffinityKey.Equals(GetDestinationAffinityKey(destinations[i])))
                    {
                        // It's allowed to affinitize a request to a pool of destinations so as to enable load-balancing among them.
                        // However, we currently stop after the first match found to avoid performance degradation.
                        matchingDestinations[0] = destinations[i];
                        break;
                    }
                }
            }
            else
            {
                Log.AffinityCannotBeEstablishedBecauseNoDestinationsFound(Logger, backendId);
            }

            // Empty destination list passed to this method is handled the same way as if no matching destinations are found.
            if (matchingDestinations[0] == null)
            {
                return new AffinityResult(null, AffinityStatus.DestinationNotFound);
            }

            return new AffinityResult(matchingDestinations, AffinityStatus.OK);
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

        protected abstract (T Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options);

        protected abstract void SetAffinityKey(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options, T unencryptedKey);

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

        protected (string Key, bool ExtractedSuccessfully) Unprotect(string encryptedRequestKey)
        {
            if (string.IsNullOrEmpty(encryptedRequestKey))
            {
                return (Key: null, ExtractedSuccessfully: true);
            }

            try
            {
                var keyBytes = Convert.FromBase64String(Pad(encryptedRequestKey));
                if (keyBytes == null)
                {
                    Log.RequestAffinityKeyCookieCannotBeDecodedFromBase64(Logger);
                    return (Key: null, ExtractedSuccessfully: false);
                }

                var decryptedKeyBytes = _dataProtector.Unprotect(keyBytes);
                if (decryptedKeyBytes == null)
                {
                    Log.RequestAffinityKeyCookieDecryptionFailed(Logger, null);
                    return (Key: null, ExtractedSuccessfully: false);
                }

                return (Key: Encoding.UTF8.GetString(decryptedKeyBytes), ExtractedSuccessfully: true);
            }
            catch (Exception ex)
            {
                Log.RequestAffinityKeyCookieDecryptionFailed(Logger, ex);
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
            private static readonly Action<ILogger, string, Exception> _affinityCannotBeEstablishedBecauseNoDestinationsFound = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnBackend,
                "The request affinity cannot be established because no destinations are found on backend `{backendId}`.");

            private static readonly Action<ILogger, string, Exception> _requestAffinityToDestinationCannotBeEstablishedBecauseAffinitizationDisabled = LoggerMessage.Define<string>(
                LogLevel.Warning,
                EventIds.RequestAffinityToDestinationCannotBeEstablishedBecauseAffinitizationDisabled,
                "The request affinity to destination `{destinationId}` cannot be established because affinitization is disabled for the backend.");

            private static readonly Action<ILogger, Exception> _requestAffinityKeyCookieDecryptionFailed = LoggerMessage.Define(
                LogLevel.Error,
                EventIds.RequestAffinityKeyCookieDecryptionFailed,
                "The request affinity key cookie decryption failed.");

            private static readonly Action<ILogger, Exception> _requestAffinityKeyCookieCannotBeDecodedFromBase64 = LoggerMessage.Define(
                LogLevel.Error,
                EventIds.RequestAffinityKeyCookieCannotBeDecodedFromBase64,
                "The request affinity key cookie cannot be decoded from Base64 representation.");

            public static void AffinityCannotBeEstablishedBecauseNoDestinationsFound(ILogger logger, string backendId)
            {
                _affinityCannotBeEstablishedBecauseNoDestinationsFound(logger, backendId, null);
            }

            public static void RequestAffinityToDestinationCannotBeEstablishedBecauseAffinitizationDisabled(ILogger logger, string destinationId)
            {
                _requestAffinityToDestinationCannotBeEstablishedBecauseAffinitizationDisabled(logger, destinationId, null);
            }

            public static void RequestAffinityKeyCookieDecryptionFailed(ILogger logger, Exception ex)
            {
                _requestAffinityKeyCookieDecryptionFailed(logger, ex);
            }

            public static void RequestAffinityKeyCookieCannotBeDecodedFromBase64(ILogger logger)
            {
                _requestAffinityKeyCookieCannotBeDecodedFromBase64(logger, null);
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class CookieSessionAffinityProvider : BaseSessionAffinityProvider<string>
    {
        private readonly CookieSessionAffinityProviderOptions _providerOptions;

        public CookieSessionAffinityProvider(
            IOptions<CookieSessionAffinityProviderOptions> providerOptions,
            IDataProtectionProvider dataProtectionProvider,
            IEnumerable<IMissingDestinationHandler> missingDestinationHandlers,
            ILogger<CookieSessionAffinityProvider> logger)
            : base(dataProtectionProvider, missingDestinationHandlers, logger)
        {
            _providerOptions = providerOptions?.Value ?? throw new ArgumentNullException(nameof(providerOptions));
        }

        public override string Mode => SessionAffinityBuiltIns.Modes.Cookie;

        protected override string GetDestinationAffinityKey(DestinationInfo destination)
        {
            return destination.DestinationId;
        }

        protected override string GetRequestAffinityKey(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options)
        {
            var encryptedRequestKey = context.Request.Cookies.TryGetValue(_providerOptions.Cookie.Name, out var keyInCookie) ? keyInCookie : null;
            return !string.IsNullOrEmpty(encryptedRequestKey) ? Unprotect(encryptedRequestKey) : null;
        }

        protected override void SetAffinityKey(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options, string unencryptedKey)
        {
            var affinityCookieOptions = _providerOptions.Cookie.Build(context);
            context.Response.Cookies.Append(_providerOptions.Cookie.Name, Protect(unencryptedKey), affinityCookieOptions);
        }

        private string Protect(string unencryptedKey)
        {
            if (string.IsNullOrEmpty(unencryptedKey))
            {
                return unencryptedKey;
            }

            var userData = Encoding.UTF8.GetBytes(unencryptedKey);

            var protectedData = DataProtector.Protect(userData);
            return Convert.ToBase64String(protectedData).TrimEnd('=');
        }

        private string Unprotect(string encryptedRequestKey)
        {
            try
            {
                var keyBytes = Convert.FromBase64String(Pad(encryptedRequestKey));
                if (keyBytes == null)
                {
                    Log.RequestAffinityKeyCookieCannotBeDecodedFromBase64(Logger);
                    return null;
                }

                var decryptedKeyBytes = DataProtector.Unprotect(keyBytes);
                if (decryptedKeyBytes == null)
                {
                    Log.RequestAffinityKeyCookieDecryptionFailed(Logger, null);
                    return null;
                }

                return Encoding.UTF8.GetString(decryptedKeyBytes);
            }
            catch (Exception ex)
            {
                Log.RequestAffinityKeyCookieDecryptionFailed(Logger, ex);
                return null;
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
            private static readonly Action<ILogger, Exception> _requestAffinityKeyCookieDecryptionFailed = LoggerMessage.Define(
                LogLevel.Error,
                EventIds.RequestAffinityKeyCookieDecryptionFailed,
                "The request affinity key cookie decryption failed.");

            private static readonly Action<ILogger, Exception> _requestAffinityKeyCookieCannotBeDecodedFromBase64 = LoggerMessage.Define(
                LogLevel.Error,
                EventIds.RequestAffinityKeyCookieCannotBeDecodedFromBase64,
                "The request affinity key cookie cannot be decoded from Base64 representation.");

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

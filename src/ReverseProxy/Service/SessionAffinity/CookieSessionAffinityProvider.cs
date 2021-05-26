// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Service.SessionAffinity
{
    internal sealed class CookieSessionAffinityProvider : BaseSessionAffinityProvider<string>
    {
        private readonly IClock _clock;

        public CookieSessionAffinityProvider(
            IDataProtectionProvider dataProtectionProvider,
            IClock clock,
            ILogger<CookieSessionAffinityProvider> logger)
            : base(dataProtectionProvider, logger)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public override string Mode => SessionAffinityConstants.Modes.Cookie;

        protected override string GetDestinationAffinityKey(DestinationState destination)
        {
            return destination.DestinationId;
        }

        protected override (string? Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, SessionAffinityConfig config)
        {
            var encryptedRequestKey = context.Request.Cookies.TryGetValue(config.AffinityKeyName!, out var keyInCookie) ? keyInCookie : null;
            return Unprotect(encryptedRequestKey);
        }

        protected override void SetAffinityKey(HttpContext context, SessionAffinityConfig config, string unencryptedKey)
        {
            var affinityCookieOptions = new CookieOptions
            {
                Path = config.Cookie?.Path ?? "/",
                SameSite = config.Cookie?.SameSite ?? SameSiteMode.Unspecified,
                HttpOnly = config.Cookie?.HttpOnly ?? true,
                MaxAge = config.Cookie?.MaxAge,
                Domain = config.Cookie?.Domain,
                IsEssential = config.Cookie?.IsEssential ?? false,
                Secure = config.Cookie?.SecurePolicy == CookieSecurePolicy.Always || (config.Cookie?.SecurePolicy == CookieSecurePolicy.SameAsRequest && context.Request.IsHttps),
                Expires = config.Cookie?.Expiration != null ? _clock.GetUtcNow().Add(config.Cookie.Expiration.Value) : default(DateTimeOffset?),
            };
            context.Response.Cookies.Append(config.AffinityKeyName!, Protect(unencryptedKey), affinityCookieOptions);
        }
    }
}

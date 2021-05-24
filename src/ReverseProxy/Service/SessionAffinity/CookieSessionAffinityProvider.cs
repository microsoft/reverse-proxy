// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
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
        private readonly ConditionalWeakTable<string, string> _defaultKeyNames = new ConditionalWeakTable<string, string>();

        public static readonly string DefaultCookieName = ".Yarp.Affinity";

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

        protected override (string? Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, ClusterConfig config)
        {
            var cookieName = config.SessionAffinity!.AffinityKeyName ?? GetUniqueDefaultKeyName(config.ClusterId);
            var encryptedRequestKey = context.Request.Cookies.TryGetValue(cookieName, out var keyInCookie) ? keyInCookie : null;
            return Unprotect(encryptedRequestKey);
        }

        protected override void SetAffinityKey(HttpContext context, string unencryptedKey, ClusterConfig config)
        {
            var affinityConfig = config.SessionAffinity!;
            var affinityCookieOptions = new CookieOptions
            {
                Path = affinityConfig.Cookie?.Path ?? "/",
                SameSite = affinityConfig.Cookie?.SameSite ?? SameSiteMode.Unspecified,
                HttpOnly = affinityConfig.Cookie?.HttpOnly ?? true,
                MaxAge = affinityConfig.Cookie?.MaxAge,
                Domain = affinityConfig.Cookie?.Domain,
                IsEssential = affinityConfig.Cookie?.IsEssential ?? false,
                Secure = affinityConfig.Cookie?.SecurePolicy == CookieSecurePolicy.Always || (affinityConfig.Cookie?.SecurePolicy == CookieSecurePolicy.SameAsRequest && context.Request.IsHttps),
                Expires = affinityConfig.Cookie?.Expiration != null ? _clock.GetUtcNow().Add(affinityConfig.Cookie.Expiration.Value) : default(DateTimeOffset?),
            };
            context.Response.Cookies.Append(affinityConfig.AffinityKeyName ?? GetUniqueDefaultKeyName(config.ClusterId), Protect(unencryptedKey), affinityCookieOptions);
        }

        private string GetUniqueDefaultKeyName(string clusterId)
        {
            return _defaultKeyNames.GetValue(clusterId, i => $"{DefaultCookieName}.{GetDefaultKeyNameSuffix(i)}");
        }
    }
}

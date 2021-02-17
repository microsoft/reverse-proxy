// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class CookieSessionAffinityProvider : BaseSessionAffinityProvider<string>
    {
        private readonly CookieSessionAffinityProviderOptions _providerOptions;

        public CookieSessionAffinityProvider(
            IOptions<CookieSessionAffinityProviderOptions> providerOptions,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<CookieSessionAffinityProvider> logger)
            : base(dataProtectionProvider, logger)
        {
            _providerOptions = providerOptions?.Value ?? throw new ArgumentNullException(nameof(providerOptions));
        }

        public override string Mode => SessionAffinityConstants.Modes.Cookie;

        protected override string GetDestinationAffinityKey(DestinationInfo destination)
        {
            return destination.DestinationId;
        }

        protected override (string Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, SessionAffinityOptions options)
        {
            var encryptedRequestKey = context.Request.Cookies.TryGetValue(_providerOptions.Cookie.Name, out var keyInCookie) ? keyInCookie : null;
            return Unprotect(encryptedRequestKey);
        }

        protected override void SetAffinityKey(HttpContext context, SessionAffinityOptions options, string unencryptedKey)
        {
            var affinityCookieOptions = _providerOptions.Cookie.Build(context);
            context.Response.Cookies.Append(_providerOptions.Cookie.Name, Protect(unencryptedKey), affinityCookieOptions);
        }
    }
}

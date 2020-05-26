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

        public override string Mode => "Cookie";

        protected override string GetDestinationAffinityKey(DestinationInfo destination)
        {
            return destination.DestinationId;
        }

        protected override string GetRequestAffinityKey(HttpContext context, BackendConfig.BackendSessionAffinityOptions options)
        {
            var encryptedRequestKey = context.Request.Cookies.TryGetValue(_providerOptions.Cookie.Name, out var keyInCookie) ? keyInCookie : null;
            return encryptedRequestKey != null ? DataProtector.Unprotect(encryptedRequestKey) : null;
        }

        protected override void SetAffinityKey(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, string unencryptedKey)
        {
            var affinityCookieOptions = _providerOptions.Cookie.Build(context);
            context.Response.Cookies.Append(_providerOptions.Cookie.Name, DataProtector.Protect(unencryptedKey), affinityCookieOptions);
        }
    }
}

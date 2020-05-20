// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class CookieSessionAffinityProvider : BaseSessionAffinityProvider<string>
    {
        private readonly CookieSessionAffinityProviderOptions _providerOptions;

        public CookieSessionAffinityProvider(IOptions<CookieSessionAffinityProviderOptions> providerOptions)
        {
            _providerOptions = providerOptions?.Value ?? throw new ArgumentNullException(nameof(providerOptions));
        }

        public override string Mode => "Cookie";

        //TBD. Add logging.

        protected override string GetDestinationAffinityKey(DestinationInfo destination)
        {
            return destination.DestinationId;
        }

        protected override string GetRequestAffinityKey(HttpContext context, BackendConfig.BackendSessionAffinityOptions options)
        {
            return context.Request.Cookies.TryGetValue(_providerOptions.Cookie.Name, out var keyInCookie) ? keyInCookie : null;
        }

        protected override void SetEncryptedAffinityKey(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, string encryptedKey)
        {
            var affinityCookieOptions = _providerOptions.Cookie.Build(context);
            // TBD. The affinity key must be encrypted.
            context.Response.Cookies.Append(_providerOptions.Cookie.Name, encryptedKey, affinityCookieOptions);
        }
    }
}

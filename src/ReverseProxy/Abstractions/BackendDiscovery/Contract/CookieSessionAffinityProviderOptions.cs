// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract
{
    /// <summary>
    /// Defines cookie-specific affinity provider options.
    /// </summary>
    public class CookieSessionAffinityProviderOptions
    {
        private CookieBuilder _cookieBuilder = new AffinityCookieBuilder();

        public static readonly string DefaultCookieName = ".Microsoft.ReverseProxy.Affinity";

        public static readonly string DefaultCookiePath = "/";

        public CookieBuilder Cookie
        {
            get => _cookieBuilder;
            set => _cookieBuilder = value ?? throw new ArgumentNullException(nameof(value));
        }

        private class AffinityCookieBuilder : CookieBuilder
        {
            public AffinityCookieBuilder()
            {
                Name = DefaultCookieName;
                Path = DefaultCookiePath;
                SecurePolicy = CookieSecurePolicy.None;
                SameSite = SameSiteMode.Lax;
                HttpOnly = true;
                IsEssential = false;
            }

            public override TimeSpan? Expiration
            {
                get => null;
                set => throw new InvalidOperationException(nameof(Expiration) + " cannot be set for the cookie defined by " + nameof(CookieSessionAffinityProviderOptions));
            }
        }
    }
}

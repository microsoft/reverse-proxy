// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Abstractions.ClusterDiscovery.Contract
{
    /// <summary>
    /// Defines cookie-specific affinity provider options.
    /// </summary>
    public class CookieSessionAffinityProviderOptions
    {
        private CookieBuilder _cookieBuilder = new AffinityCookieBuilder();

        public static readonly string DefaultCookieName = ".Yarp.ReverseProxy.Affinity";

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
                SecurePolicy = CookieSecurePolicy.None;
                SameSite = SameSiteMode.Unspecified;
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

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.SessionAffinity;

internal static class AffinityHelpers
{
    internal static CookieOptions CreateCookieOptions(SessionAffinityCookieConfig? config, bool isHttps, IClock clock)
    {
        return new CookieOptions
        {
            Path = config?.Path ?? "/",
            SameSite = config?.SameSite ?? SameSiteMode.Unspecified,
            HttpOnly = config?.HttpOnly ?? true,
            MaxAge = config?.MaxAge,
            Domain = config?.Domain,
            IsEssential = config?.IsEssential ?? false,
            Secure = config?.SecurePolicy == CookieSecurePolicy.Always || (config?.SecurePolicy == CookieSecurePolicy.SameAsRequest && isHttps),
            Expires = config?.Expiration is not null ? clock.GetUtcNow().Add(config.Expiration.Value) : default(DateTimeOffset?),
        };
    }
}

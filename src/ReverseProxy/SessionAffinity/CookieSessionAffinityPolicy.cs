// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.SessionAffinity;

internal sealed class CookieSessionAffinityPolicy : BaseEncryptedSessionAffinityPolicy<string>
{
    private readonly IClock _clock;

    public CookieSessionAffinityPolicy(
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        ILogger<CookieSessionAffinityPolicy> logger)
        : base(dataProtectionProvider, logger)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public override string Name => SessionAffinityConstants.Policies.Cookie;

    protected override string GetDestinationAffinityKey(DestinationState destination)
    {
        return destination.DestinationId;
    }

    protected override (string? Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, ClusterState cluster, SessionAffinityConfig config)
    {
        var encryptedRequestKey = context.Request.Cookies.TryGetValue(config.AffinityKeyName, out var keyInCookie) ? keyInCookie : null;
        return Unprotect(encryptedRequestKey);
    }

    protected override void SetAffinityKey(HttpContext context, ClusterState cluster, SessionAffinityConfig config, string unencryptedKey)
    {
        var affinityCookieOptions = AffinityHelpers.CreateCookieOptions(config.Cookie, context.Request.IsHttps, _clock);
        context.Response.Cookies.Append(config.AffinityKeyName, Protect(unencryptedKey), affinityCookieOptions);
    }
}

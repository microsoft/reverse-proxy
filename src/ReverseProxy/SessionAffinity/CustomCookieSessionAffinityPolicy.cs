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

internal sealed class CustomCookieSessionAffinityPolicy : CookieSessionAffinityPolicy
{
    public CustomCookieSessionAffinityPolicy(
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        ILogger<CustomCookieSessionAffinityPolicy> logger)
        : base(dataProtectionProvider, clock, logger)
    {
    }

    public override string Name => "CustomCookie";

    protected override CookieOptions CreateCookieOptions(HttpContext context, ClusterState cluster, SessionAffinityConfig config)
    {
        var options = base.CreateCookieOptions(context, cluster, config);
        options.Domain = context.Request.Host.Host;
        return options;
    }
}

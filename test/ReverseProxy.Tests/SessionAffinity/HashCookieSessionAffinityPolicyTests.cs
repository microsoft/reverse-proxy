// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.SessionAffinity.Tests;

public class HashCookieSessionAffinityPolicyTests
{
    private readonly SessionAffinityConfig _config = new()
    {
        Enabled = true,
        Policy = "HashCookie",
        FailurePolicy = "Return503Error",
        AffinityKeyName = "My.Affinity",
        Cookie = new SessionAffinityCookieConfig
        {
            Domain = "mydomain.my",
            HttpOnly = false,
            IsEssential = true,
            MaxAge = TimeSpan.FromHours(1),
            Path = "/some",
            SameSite = SameSiteMode.Lax,
            SecurePolicy = CookieSecurePolicy.Always,
        }
    };
    private readonly IReadOnlyList<DestinationState> _destinations = new[] { new DestinationState("dest-A"), new DestinationState("dest-B"), new DestinationState("dest-C") };

    [Fact]
    public void FindAffinitizedDestination_AffinityKeyIsNotSetOnRequest_ReturnKeyNotSet()
    {
        var policy = new HashCookieSessionAffinityPolicy(
            new ManualClock(),
            NullLogger<HashCookieSessionAffinityPolicy>.Instance);

        Assert.Equal(SessionAffinityConstants.Policies.HashCookie, policy.Name);

        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = new[] { $"Some-Cookie=ZZZ" };
        var cluster = new ClusterState("cluster");

        var affinityResult = policy.FindAffinitizedDestinations(context, cluster, _config, _destinations);

        Assert.Equal(AffinityStatus.AffinityKeyNotSet, affinityResult.Status);
        Assert.Null(affinityResult.Destinations);
    }

    [Fact]
    public void FindAffinitizedDestination_AffinityKeyIsSetOnRequest_Success()
    {
        var policy = new HashCookieSessionAffinityPolicy(
            new ManualClock(),
            NullLogger<HashCookieSessionAffinityPolicy>.Instance);
        var context = new DefaultHttpContext();
        var affinitizedDestination = _destinations[1];
        context.Request.Headers["Cookie"] = GetCookieWithAffinity(affinitizedDestination);
        var cluster = new ClusterState("cluster");

        var affinityResult = policy.FindAffinitizedDestinations(context, cluster, _config, _destinations);

        Assert.Equal(AffinityStatus.OK, affinityResult.Status);
        Assert.Equal(1, affinityResult.Destinations.Count);
        Assert.Same(affinitizedDestination, affinityResult.Destinations[0]);
    }

    [Fact]
    public void AffinitizedRequest_CustomConfigAffinityKeyIsNotExtracted_SetKeyOnResponse()
    {
        var policy = new HashCookieSessionAffinityPolicy(
            new ManualClock(),
            NullLogger<HashCookieSessionAffinityPolicy>.Instance);
        var context = new DefaultHttpContext();

        policy.AffinitizeResponse(context, new ClusterState("cluster"), _config, _destinations[1]);

        var affinityCookieHeader = context.Response.Headers["Set-Cookie"];
        Assert.Equal("My.Affinity=53c079ed4c377b0d; max-age=3600; domain=mydomain.my; path=/some; secure; samesite=lax",
            affinityCookieHeader);
    }

    [Fact]
    public void AffinitizeRequest_CookieConfigSpecified_UseIt()
    {
        var policy = new HashCookieSessionAffinityPolicy(
            new ManualClock(),
            NullLogger<HashCookieSessionAffinityPolicy>.Instance);
        var context = new DefaultHttpContext();

        policy.AffinitizeResponse(context, new ClusterState("cluster"), _config, _destinations[1]);

        var affinityCookieHeader = context.Response.Headers["Set-Cookie"];
        Assert.Equal("My.Affinity=53c079ed4c377b0d; max-age=3600; domain=mydomain.my; path=/some; secure; samesite=lax",
            affinityCookieHeader);
    }

    [Fact]
    public void AffinitizedRequest_AffinityKeyIsExtracted_DoNothing()
    {
        var policy = new HashCookieSessionAffinityPolicy(
            new ManualClock(),
            NullLogger<HashCookieSessionAffinityPolicy>.Instance);
        var context = new DefaultHttpContext();
        var affinitizedDestination = _destinations[0];
        context.Request.Headers["Cookie"] = GetCookieWithAffinity(affinitizedDestination);
        var cluster = new ClusterState("cluster");

        var affinityResult = policy.FindAffinitizedDestinations(context, cluster, _config, _destinations);

        Assert.Equal(AffinityStatus.OK, affinityResult.Status);

        policy.AffinitizeResponse(context, cluster, _config, affinitizedDestination);

        Assert.False(context.Response.Headers.ContainsKey("Cookie"));
    }

    private string[] GetCookieWithAffinity(DestinationState affinitizedDestination)
    {
        var destinationIdBytes = Encoding.Unicode.GetBytes(affinitizedDestination.DestinationId.ToUpperInvariant());
        var hashBytes = XxHash64.Hash(destinationIdBytes);
        var value = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return new[] { $"Some-Cookie=ZZZ", $"{_config.AffinityKeyName}={value}" };
    }
}

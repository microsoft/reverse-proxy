// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Common.Tests;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.SessionAffinity
{
    public class CookieSessionAffinityProviderTests
    {
        private const string ClusterId = "cluster1";
        private readonly ClusterConfig _defaultConfig = new ClusterConfig
        {
            ClusterId = ClusterId,
            SessionAffinity = new SessionAffinityConfig
            {
                Enabled = true,
                Mode = "Cookie",
                FailurePolicy = "Return503",
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
            }
        };
        private readonly IReadOnlyList<DestinationState> _destinations = new[] { new DestinationState("dest-A"), new DestinationState("dest-B"), new DestinationState("dest-C") };

        [Fact]
        public void FindAffinitizedDestination_AffinityKeyIsNotSetOnRequest_ReturnKeyNotSet()
        {
            var provider = new CookieSessionAffinityProvider(
                AffinityTestHelper.GetDataProtector().Object,
                new ManualClock(),
                AffinityTestHelper.GetLogger<CookieSessionAffinityProvider>().Object);

            Assert.Equal(SessionAffinityConstants.Modes.Cookie, provider.Mode);

            var context = new DefaultHttpContext();
            context.Request.Headers["Cookie"] = new[] { $"Some-Cookie=ZZZ" };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, _defaultConfig);

            Assert.Equal(AffinityStatus.AffinityKeyNotSet, affinityResult.Status);
            Assert.Null(affinityResult.Destinations);
        }

        [Fact]
        public void FindAffinitizedDestination_AffinityKeyIsSetOnRequest_Success()
        {
            var provider = new CookieSessionAffinityProvider(
                AffinityTestHelper.GetDataProtector().Object,
                new ManualClock(),
                AffinityTestHelper.GetLogger<CookieSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            var affinitizedDestination = _destinations[1];
            context.Request.Headers["Cookie"] = GetCookieWithAffinity(affinitizedDestination);

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, _defaultConfig);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);
            Assert.Equal(1, affinityResult.Destinations.Count);
            Assert.Same(affinitizedDestination, affinityResult.Destinations[0]);
        }

        [Fact]
        public void AffinitizedRequest_DefaultConfigAffinityKeyIsNotExtracted_SetKeyOnResponse()
        {
            var provider = new CookieSessionAffinityProvider(
                AffinityTestHelper.GetDataProtector().Object,
                new ManualClock(),
                AffinityTestHelper.GetLogger<CookieSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();

            var newAffinityConfig = _defaultConfig.SessionAffinity with { AffinityKeyName = null, Cookie = null };
            var config = _defaultConfig with { SessionAffinity = newAffinityConfig };
            provider.AffinitizeRequest(context, _destinations[1], config);

            var affinityCookieHeader = context.Response.Headers["Set-Cookie"];
            Assert.Equal(".Yarp.Affinity.oUB5HSsgqEfyx0xi=ZGVzdC1C; path=/; httponly", affinityCookieHeader);
        }

        [Fact]
        public void AffinitizedRequest_CustomConfigAffinityKeyIsNotExtracted_SetKeyOnResponse()
        {
            var provider = new CookieSessionAffinityProvider(
                AffinityTestHelper.GetDataProtector().Object,
                new ManualClock(),
                AffinityTestHelper.GetLogger<CookieSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();

            provider.AffinitizeRequest(context, _destinations[1], _defaultConfig);

            var affinityCookieHeader = context.Response.Headers["Set-Cookie"];
            Assert.Equal("My.Affinity=ZGVzdC1C; max-age=3600; domain=mydomain.my; path=/some; secure; samesite=lax", affinityCookieHeader);
        }

        [Fact]
        public void AffinitizeRequest_CookieConfigSpecified_UseIt()
        {
            var provider = new CookieSessionAffinityProvider(
                AffinityTestHelper.GetDataProtector().Object,
                new ManualClock(),
                AffinityTestHelper.GetLogger<CookieSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();

            provider.AffinitizeRequest(context, _destinations[1], _defaultConfig);

            var affinityCookieHeader = context.Response.Headers["Set-Cookie"];
            Assert.Equal("My.Affinity=ZGVzdC1C; max-age=3600; domain=mydomain.my; path=/some; secure; samesite=lax", affinityCookieHeader);
        }

        [Fact]
        public void AffinitizedRequest_AffinityKeyIsExtracted_DoNothing()
        {
            var provider = new CookieSessionAffinityProvider(
                AffinityTestHelper.GetDataProtector().Object,
                new ManualClock(),
                AffinityTestHelper.GetLogger<CookieSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            var affinitizedDestination = _destinations[0];
            context.Request.Headers["Cookie"] = GetCookieWithAffinity(affinitizedDestination);

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, _defaultConfig);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);

            provider.AffinitizeRequest(context, affinitizedDestination, _defaultConfig);

            Assert.False(context.Response.Headers.ContainsKey("Cookie"));
        }

        private string[] GetCookieWithAffinity(DestinationState affinitizedDestination)
        {
            return new[] { $"Some-Cookie=ZZZ", $"{_defaultConfig.SessionAffinity.AffinityKeyName}={affinitizedDestination.DestinationId.ToUTF8BytesInBase64()}" };
        }
    }
}

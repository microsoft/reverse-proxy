// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.SessionAffinity
{
    public class CookieSessionAffinityProviderTests
    {
        private readonly CookieSessionAffinityProviderOptions _defaultProviderOptions = new CookieSessionAffinityProviderOptions();
        private readonly SessionAffinityOptions _defaultOptions = new SessionAffinityOptions
        {
            Enabled = true,
            Mode = "Cookie",
            FailurePolicy = "Return503",
        };
        private readonly IReadOnlyList<DestinationInfo> _destinations = new[] { new DestinationInfo("dest-A"), new DestinationInfo("dest-B"), new DestinationInfo("dest-C") };

        [Fact]
        public void FindAffinitizedDestination_AffinityKeyIsNotSetOnRequest_ReturnKeyNotSet()
        {
            var provider = new CookieSessionAffinityProvider(
                Options.Create(_defaultProviderOptions),
                AffinityTestHelper.GetDataProtector().Object,
                AffinityTestHelper.GetLogger<CookieSessionAffinityProvider>().Object);

            Assert.Equal(SessionAffinityConstants.Modes.Cookie, provider.Mode);

            var context = new DefaultHttpContext();
            context.Request.Headers["Cookie"] = new[] { $"Some-Cookie=ZZZ" };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, "cluster-1", _defaultOptions);

            Assert.Equal(AffinityStatus.AffinityKeyNotSet, affinityResult.Status);
            Assert.Null(affinityResult.Destinations);
        }

        [Fact]
        public void FindAffinitizedDestination_AffinityKeyIsSetOnRequest_Success()
        {
            var provider = new CookieSessionAffinityProvider(
                Options.Create(_defaultProviderOptions),
                AffinityTestHelper.GetDataProtector().Object,
                AffinityTestHelper.GetLogger<CookieSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            var affinitizedDestination = _destinations[1];
            context.Request.Headers["Cookie"] = GetCookieWithAffinity(affinitizedDestination);

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, "cluster-1", _defaultOptions);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);
            Assert.Equal(1, affinityResult.Destinations.Count);
            Assert.Same(affinitizedDestination, affinityResult.Destinations[0]);
        }

        [Fact]
        public void AffinitizedRequest_AffinityKeyIsNotExtracted_SetKeyOnResponse()
        {
            var provider = new CookieSessionAffinityProvider(
                Options.Create(_defaultProviderOptions),
                AffinityTestHelper.GetDataProtector().Object,
                AffinityTestHelper.GetLogger<CookieSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();

            provider.AffinitizeRequest(context, _defaultOptions, _destinations[1]);

            var affinityCookieHeader = context.Response.Headers["Set-Cookie"];
            Assert.Equal(".Yarp.ReverseProxy.Affinity=ZGVzdC1C; path=/; httponly", affinityCookieHeader);
        }

        [Fact]
        public void AffinitizeRequest_CookieBuilderSettingsChanged_UseNewSettings()
        {
            var providerOptions = new CookieSessionAffinityProviderOptions();
            providerOptions.Cookie.Domain = "mydomain.my";
            providerOptions.Cookie.HttpOnly = false;
            providerOptions.Cookie.IsEssential = true;
            providerOptions.Cookie.MaxAge = TimeSpan.FromHours(1);
            providerOptions.Cookie.Name = "My.Affinity";
            providerOptions.Cookie.Path = "/some";
            providerOptions.Cookie.SameSite = SameSiteMode.Lax;
            providerOptions.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            var provider = new CookieSessionAffinityProvider(
                Options.Create(providerOptions),
                AffinityTestHelper.GetDataProtector().Object,
                AffinityTestHelper.GetLogger<CookieSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();

            provider.AffinitizeRequest(context, _defaultOptions, _destinations[1]);

            var affinityCookieHeader = context.Response.Headers["Set-Cookie"];
            Assert.Equal("My.Affinity=ZGVzdC1C; max-age=3600; domain=mydomain.my; path=/some; secure; samesite=lax", affinityCookieHeader);
        }

        [Fact]
        public void AffinitizedRequest_AffinityKeyIsExtracted_DoNothing()
        {
            var provider = new CookieSessionAffinityProvider(
                Options.Create(_defaultProviderOptions),
                AffinityTestHelper.GetDataProtector().Object,
                AffinityTestHelper.GetLogger<CookieSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            var affinitizedDestination = _destinations[0];
            context.Request.Headers["Cookie"] = GetCookieWithAffinity(affinitizedDestination);

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, "cluster-1", _defaultOptions);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);

            provider.AffinitizeRequest(context, _defaultOptions, affinitizedDestination);

            Assert.False(context.Response.Headers.ContainsKey("Cookie"));
        }

        private string[] GetCookieWithAffinity(DestinationInfo affinitizedDestination)
        {
            return new[] { $"Some-Cookie=ZZZ", $"{_defaultProviderOptions.Cookie.Name}={affinitizedDestination.DestinationId.ToUTF8BytesInBase64()}" };
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    public class CookieSessionAffinityProviderTests
    {
        private readonly CookieSessionAffinityProviderOptions _defaultProviderOptions = new CookieSessionAffinityProviderOptions();
        private readonly BackendConfig.BackendSessionAffinityOptions _defaultOptions = new BackendConfig.BackendSessionAffinityOptions(true, "Cookie", "Return503", null);
        private readonly IReadOnlyList<DestinationInfo> _destinations = new[] { new DestinationInfo("dest-A"), new DestinationInfo("dest-B"), new DestinationInfo("dest-C") };

        [Fact]
        public void FindAffinitizedDestination_AffinityKeyIsNotSetOnRequest_ReturnKeyNotSet()
        {
            var provider = new CookieSessionAffinityProvider(Options.Create(_defaultProviderOptions), GetDataProtector().Object, GetLogger().Object);
            var context = new DefaultHttpContext();
            context.Request.Headers["Cookie"] = new[] { $"Some-Cookie=ZZZ" };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, "backend-1", _defaultOptions);

            Assert.Equal(AffinityStatus.AffinityKeyNotSet, affinityResult.Status);
            Assert.Null(affinityResult.Destinations);
        }

        [Fact]
        public void FindAffinitizedDestination_AffinityKeyIsSetOnRequest_Success()
        {
            var provider = new CookieSessionAffinityProvider(Options.Create(_defaultProviderOptions), GetDataProtector().Object, GetLogger().Object);
            var context = new DefaultHttpContext();
            var affinitizedDestination = _destinations[1];
            context.Request.Headers["Cookie"] = GetCookieWithAffinity(affinitizedDestination);

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, "backend-1", _defaultOptions);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);
            Assert.Equal(1, affinityResult.Destinations.Count);
            Assert.Equal(affinitizedDestination, affinityResult.Destinations[0]);
        }

        [Fact]
        public void AffinitizedRequest_AffinityKeyIsNotExtracted_SetKeyOnResponse()
        {
            var provider = new CookieSessionAffinityProvider(Options.Create(_defaultProviderOptions), GetDataProtector().Object, GetLogger().Object);
            var context = new DefaultHttpContext();

            provider.AffinitizeRequest(context, _defaultOptions, _destinations[1]);

            var affinityCookieHeader = context.Response.Headers["Set-Cookie"];
            Assert.Equal(".Microsoft.ReverseProxy.Affinity=ZGVzdC1C; path=/; httponly", affinityCookieHeader);
        }

        [Fact]
        public void AffinitizedRequest_AffinityKeyIsExtracted_DoNothing()
        {
            var provider = new CookieSessionAffinityProvider(Options.Create(_defaultProviderOptions), GetDataProtector().Object, GetLogger().Object);
            var context = new DefaultHttpContext();
            var affinitizedDestination = _destinations[0];
            context.Request.Headers["Cookie"] = GetCookieWithAffinity(affinitizedDestination);

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, "backend-1", _defaultOptions);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);

            provider.AffinitizeRequest(context, _defaultOptions, affinitizedDestination);

            Assert.False(context.Response.Headers.ContainsKey("Cookie"));
        }

        private string[] GetCookieWithAffinity(DestinationInfo affinitizedDestination)
        {
            return new[] { $"Some-Cookie=ZZZ", $"{_defaultProviderOptions.Cookie.Name}={Convert.ToBase64String(Encoding.UTF8.GetBytes(affinitizedDestination.DestinationId))}" };
        }

        private static Mock<ILogger<CookieSessionAffinityProvider>> GetLogger()
        {
            return new Mock<ILogger<CookieSessionAffinityProvider>>();
        }

        private static Mock<IDataProtector> GetDataProtector()
        {
            var protector = new Mock<IDataProtector>();
            protector.Setup(p => p.CreateProtector(It.IsAny<string>())).Returns(protector.Object);
            protector.Setup(p => p.Protect(It.IsAny<byte[]>())).Returns((byte[] b) => b);
            protector.Setup(p => p.Unprotect(It.IsAny<byte[]>())).Returns((byte[] b) => b);
            return protector;
        }
    }
}

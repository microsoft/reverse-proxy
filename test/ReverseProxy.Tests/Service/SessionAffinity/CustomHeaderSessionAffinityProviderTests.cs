// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;
using Xunit;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    public class CustomHeaderSessionAffinityProviderTests
    {
        private const string AffinityHeaderName = "X-MyAffinity";
        private readonly BackendConfig.BackendSessionAffinityOptions _defaultOptions =
            new BackendConfig.BackendSessionAffinityOptions(true, "Cookie", "Return503", new Dictionary<string, string> { { "CustomHeaderName", AffinityHeaderName } });
        private readonly IReadOnlyList<DestinationInfo> _destinations = new[] { new DestinationInfo("dest-A"), new DestinationInfo("dest-B"), new DestinationInfo("dest-C") };

        [Fact]
        public void FindAffinitizedDestination_AffinityKeyIsNotSetOnRequest_ReturnKeyNotSet()
        {
            var provider = new CustomHeaderSessionAffinityProvider(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);

            Assert.Equal(SessionAffinityBuiltIns.Modes.CustomHeader, provider.Mode);

            var context = new DefaultHttpContext();
            context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, "backend-1", _defaultOptions);

            Assert.Equal(AffinityStatus.AffinityKeyNotSet, affinityResult.Status);
            Assert.Null(affinityResult.Destinations);
        }

        [Fact]
        public void FindAffinitizedDestination_AffinityKeyIsSetOnRequest_Success()
        {
            var provider = new CustomHeaderSessionAffinityProvider(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };
            var affinitizedDestination = _destinations[1];
            context.Request.Headers[AffinityHeaderName] = new[] { affinitizedDestination.DestinationId.ToUTF8BytesInBase64() };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, "backend-1", _defaultOptions);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);
            Assert.Equal(1, affinityResult.Destinations.Count);
            Assert.Equal(affinitizedDestination, affinityResult.Destinations[0]);
        }

        [Fact]
        public void FindAffinitizedDestination_CustomHeaderNameIsNotSpecified_Throw()
        {
            var options = new BackendConfig.BackendSessionAffinityOptions(true, "CustomHeader", "Return503", null);
            var provider = new CustomHeaderSessionAffinityProvider(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };

            Assert.Throws<ArgumentException>(() => provider.FindAffinitizedDestinations(context, _destinations, "backend-1", options));
        }

        [Fact]
        public void AffinitizedRequest_AffinityKeyIsNotExtracted_SetKeyOnResponse()
        {
            var provider = new CustomHeaderSessionAffinityProvider(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            var chosenDestination = _destinations[1];
            var expectedAffinityHeaderValue = chosenDestination.DestinationId.ToUTF8BytesInBase64();

            provider.AffinitizeRequest(context, _defaultOptions, chosenDestination);

            Assert.True(context.Response.Headers.ContainsKey(AffinityHeaderName));
            Assert.Equal(expectedAffinityHeaderValue, context.Response.Headers[AffinityHeaderName]);
        }

        [Fact]
        public void AffinitizedRequest_AffinityKeyIsExtracted_DoNothing()
        {
            var provider = new CustomHeaderSessionAffinityProvider(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };
            var affinitizedDestination = _destinations[1];
            context.Request.Headers[AffinityHeaderName] = new[] { affinitizedDestination.DestinationId.ToUTF8BytesInBase64() };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, "backend-1", _defaultOptions);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);

            provider.AffinitizeRequest(context, _defaultOptions, affinitizedDestination);

            Assert.False(context.Response.Headers.ContainsKey(AffinityHeaderName));
        }
    }
}

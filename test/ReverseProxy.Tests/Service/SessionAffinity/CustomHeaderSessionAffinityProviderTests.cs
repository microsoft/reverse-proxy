// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;
using Xunit;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    public class CustomHeaderSessionAffinityProviderTests
    {
        private const string MyAffinityHeaderName = "X-MyAffinity";
        private readonly CustomHeaderSessionAffinityProviderOptions _defaultProviderOptions = new CustomHeaderSessionAffinityProviderOptions();
        private readonly BackendConfig.BackendSessionAffinityOptions _defaultOptions = new BackendConfig.BackendSessionAffinityOptions(true, "CustomHeader", "Return503", null);
        private readonly IReadOnlyList<DestinationInfo> _destinations = new[] { new DestinationInfo("dest-A"), new DestinationInfo("dest-B"), new DestinationInfo("dest-C") };

        [Fact]
        public void FindAffinitizedDestination_AffinityKeyIsNotSetOnRequest_ReturnKeyNotSet()
        {
            var provider = new CustomHeaderSessionAffinityProvider(
                Options.Create(_defaultProviderOptions),
                AffinityTestHelper.GetDataProtector().Object,
                AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);

            Assert.Equal(SessionAffinityConstants.Modes.CustomHeader, provider.Mode);

            var context = new DefaultHttpContext();
            context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, "backend-1", _defaultOptions);

            Assert.Equal(AffinityStatus.AffinityKeyNotSet, affinityResult.Status);
            Assert.Null(affinityResult.Destinations);
        }

        [Theory]
        [MemberData(nameof(FindAffinitizedDestination_AffinityKeyIsSetOnRequest_Cases))]
        public void FindAffinitizedDestination_AffinityKeyIsSetOnRequest_Success(string headerName)
        {
            var providerOptions = new CustomHeaderSessionAffinityProviderOptions();

            if (headerName != null)
            {
                providerOptions.CustomHeaderName = headerName;
            }

            var provider = new CustomHeaderSessionAffinityProvider(
                Options.Create(providerOptions),
                AffinityTestHelper.GetDataProtector().Object,
                AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };
            var affinitizedDestination = _destinations[1];
            context.Request.Headers[providerOptions.CustomHeaderName] = new[] { affinitizedDestination.DestinationId.ToUTF8BytesInBase64() };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, "backend-1", _defaultOptions);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);
            Assert.Equal(1, affinityResult.Destinations.Count);
            Assert.Same(affinitizedDestination, affinityResult.Destinations[0]);
        }

        [Fact]
        public void Ctor_ProviderOptionsIsNull_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new CustomHeaderSessionAffinityProvider(
                null,
                AffinityTestHelper.GetDataProtector().Object,
                AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object));
        }

        [Fact]
        public void AffinitizedRequest_AffinityKeyIsNotExtracted_SetKeyOnResponse()
        {
            var provider = new CustomHeaderSessionAffinityProvider(
                Options.Create(_defaultProviderOptions),
                AffinityTestHelper.GetDataProtector().Object,
                AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            var chosenDestination = _destinations[1];
            var expectedAffinityHeaderValue = chosenDestination.DestinationId.ToUTF8BytesInBase64();

            provider.AffinitizeRequest(context, _defaultOptions, chosenDestination);

            Assert.True(context.Response.Headers.ContainsKey(CustomHeaderSessionAffinityProviderOptions.DefaultCustomHeaderName));
            Assert.Equal(expectedAffinityHeaderValue, context.Response.Headers[CustomHeaderSessionAffinityProviderOptions.DefaultCustomHeaderName]);
        }

        [Fact]
        public void AffinitizedRequest_AffinityKeyIsExtracted_DoNothing()
        {
            var provider = new CustomHeaderSessionAffinityProvider(
                Options.Create(_defaultProviderOptions),
                AffinityTestHelper.GetDataProtector().Object,
                AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };
            var affinitizedDestination = _destinations[1];
            context.Request.Headers[CustomHeaderSessionAffinityProviderOptions.DefaultCustomHeaderName] = new[] { affinitizedDestination.DestinationId.ToUTF8BytesInBase64() };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, "backend-1", _defaultOptions);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);

            provider.AffinitizeRequest(context, _defaultOptions, affinitizedDestination);

            Assert.False(context.Response.Headers.ContainsKey(CustomHeaderSessionAffinityProviderOptions.DefaultCustomHeaderName));
        }

        public static IEnumerable<object[]> FindAffinitizedDestination_AffinityKeyIsSetOnRequest_Cases()
        {
            // Use the default custom header name
            yield return new object[] { null };
            yield return new object[] { MyAffinityHeaderName };
        }
    }
}

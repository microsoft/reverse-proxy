// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    public class BaseSesstionAffinityProviderTests : TestAutoMockBase
    {
        private const string InvalidKeyNull = "!invalid key - null!";
        private const string InvalidKeyThrow = "!invalid key - throw!";
        private const string KeyName = "StubAffinityKey";
        private readonly BackendConfig.BackendSessionAffinityOptions _defaultOptions = new BackendConfig.BackendSessionAffinityOptions(true, "Stub", "Return503", new Dictionary<string, string> { { "AffinityKeyName", KeyName } });

        [Theory]
        [MemberData(nameof(FindAffinitizedDestinationsCases))]
        public void Request_FindAffinitizedDestinations(
            HttpContext context,
            DestinationInfo[] allDestinations,
            AffinityStatus expectedStatus,
            DestinationInfo expectedDestination,
            byte[] expectedEncryptedKey,
            bool unprotectCalled)
        {
            var dataProtector = GetDataProtector();
            var provider = new ProviderStub(dataProtector.Object, Mock<ILogger>().Object);
            var affinityResult = provider.FindAffinitizedDestinations(context, allDestinations, "backend-1", _defaultOptions);

            if(unprotectCalled)
            {
                dataProtector.Verify(p => p.Unprotect(It.Is<byte[]>(b => b.SequenceEqual(expectedEncryptedKey))), Times.Once);
            }

            Assert.Equal(expectedStatus, affinityResult.Status);
            Assert.Same(expectedDestination, affinityResult.Destinations?.FirstOrDefault());
            if (expectedDestination != null)
            {
                Assert.Equal(1, affinityResult.Destinations.Count);
            }
            else
            {
                Assert.Null(affinityResult.Destinations);
            }
        }

        // Current version of test SDK cannot properly handle Debug.Fail, so the tests are skipped in Debug
#if RELEASE
        [Fact]
        public void FindAffinitizedDestination_AffinityDisabledOnBackend_ReturnsAffinityDisabled()
        {
            var provider = new ProviderStub(GetDataProtector().Object, Mock<ILogger>().Object);
            var options = new BackendConfig.BackendSessionAffinityOptions(false, _defaultOptions.Mode, _defaultOptions.AffinityFailurePolicy, _defaultOptions.Settings);
            var affinityResult = provider.FindAffinitizedDestinations(new DefaultHttpContext(), new[] { new DestinationInfo("1") }, "backend-1", options);
            Assert.Equal(AffinityStatus.AffinityDisabled, affinityResult.Status);
            Assert.Null(affinityResult.Destinations);
        }

        [Fact]
        public void AffinitizeRequest_AffinitiDisabled_DoNothing()
        {
            var dataProtector = GetDataProtector();
            var provider = new ProviderStub(dataProtector.Object, Mock<ILogger>().Object);
            provider.AffinitizeRequest(new DefaultHttpContext(), default, new DestinationInfo("id"));
            Assert.Null(provider.LastSetEncryptedKey);
            dataProtector.Verify(p => p.Protect(It.IsAny<byte[]>()), Times.Never);
        }
#endif

        [Fact]
        public void AffinitizeRequest_RequestIsAffinitized_DoNothing()
        {
            var dataProtector = GetDataProtector();
            var provider = new ProviderStub(dataProtector.Object, Mock<ILogger>().Object);
            var context = new DefaultHttpContext();
            provider.DirectlySetExtractedKeyOnContext(context, "ExtractedKey");
            provider.AffinitizeRequest(context, _defaultOptions, new DestinationInfo("id"));
            Assert.Null(provider.LastSetEncryptedKey);
            dataProtector.Verify(p => p.Protect(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void AffinitizeRequest_RequestIsNotAffinitized_SetAffinityKey()
        {
            var dataProtector = GetDataProtector();
            var provider = new ProviderStub(dataProtector.Object, Mock<ILogger>().Object);
            var destination = new DestinationInfo("dest-A");
            provider.AffinitizeRequest(new DefaultHttpContext(), _defaultOptions, destination);
            Assert.Equal("ZGVzdC1B", provider.LastSetEncryptedKey);
            var keyBytes = Encoding.UTF8.GetBytes(destination.DestinationId);
            dataProtector.Verify(p => p.Protect(It.Is<byte[]>(b => b.SequenceEqual(keyBytes))), Times.Once);
        }

        [Fact]
        public void FindAffinitizedDestinations_AffinityOptionSettingNotFound_Throw()
        {
            var provider = new ProviderStub(GetDataProtector().Object, Mock<ILogger>().Object);
            var options = GetOptionsWithUnknownSetting();
            Assert.Throws<ArgumentException>(() => provider.FindAffinitizedDestinations(new DefaultHttpContext(), new[] { new DestinationInfo("dest-A") }, "backend-1", options));
        }

        [Fact]
        public void AffinitizeRequest_AffinityOptionSettingNotFound_Throw()
        {
            var provider = new ProviderStub(GetDataProtector().Object, Mock<ILogger>().Object);
            var options = GetOptionsWithUnknownSetting();
            Assert.Throws<ArgumentException>(() => provider.AffinitizeRequest(new DefaultHttpContext(), options, new DestinationInfo("dest-A")));
        }

        [Fact]
        public void Ctor_MandatoryArgumentIsNull_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ProviderStub(null, Mock<ILogger>().Object));
            // CreateDataProtector will return null
            Assert.Throws<ArgumentNullException>(() => new ProviderStub(Mock<IDataProtector>().Object, Mock<ILogger>().Object));
            Assert.Throws<ArgumentNullException>(() => new ProviderStub(GetDataProtector().Object, null));
        }

        public static IEnumerable<object[]> FindAffinitizedDestinationsCases()
        {
            var destinations = new[] { new DestinationInfo("dest-A"), new DestinationInfo("dest-B"), new DestinationInfo("dest-C") };
            yield return new object[] { GetHttpContext(new[] { ("SomeKey", "SomeValue") }), destinations, AffinityStatus.AffinityKeyNotSet, null, null, false };
            yield return new object[] { GetHttpContext(new[] { (KeyName, "dest-B") }), destinations, AffinityStatus.OK, destinations[1], Encoding.UTF8.GetBytes("dest-B"), true };
            yield return new object[] { GetHttpContext(new[] { (KeyName, "dest-Z") }), destinations, AffinityStatus.DestinationNotFound, null, Encoding.UTF8.GetBytes("dest-Z"), true };
            yield return new object[] { GetHttpContext(new[] { (KeyName, "dest-B") }), new DestinationInfo[0], AffinityStatus.DestinationNotFound, null, Encoding.UTF8.GetBytes("dest-B"), true };
            yield return new object[] { GetHttpContext(new[] { (KeyName, InvalidKeyNull) }), destinations, AffinityStatus.AffinityKeyExtractionFailed, null, Encoding.UTF8.GetBytes(InvalidKeyNull), true };
            yield return new object[] { GetHttpContext(new[] { (KeyName, InvalidKeyThrow) }), destinations, AffinityStatus.AffinityKeyExtractionFailed, null, Encoding.UTF8.GetBytes(InvalidKeyThrow), true };
        }

        private static BackendConfig.BackendSessionAffinityOptions GetOptionsWithUnknownSetting()
        {
            return new BackendConfig.BackendSessionAffinityOptions(true, "Stub", "Return503", new Dictionary<string, string> { { "Unknown", "ZZZ" } });
        }

        private static HttpContext GetHttpContext((string Key, string Value)[] items)
        {
            var context = new DefaultHttpContext
            {
                Items = items.ToDictionary(i => (object)i.Key, i => (object)Convert.ToBase64String(Encoding.UTF8.GetBytes(i.Value)))
            };
            return context;
        }

        private Mock<IDataProtector> GetDataProtector()
        {
            var result = Mock<IDataProtector>();
            var nullBytes = Encoding.UTF8.GetBytes(InvalidKeyNull);
            var throwBytes = Encoding.UTF8.GetBytes(InvalidKeyThrow);
            result.Setup(p => p.Protect(It.IsAny<byte[]>())).Returns((byte[] k) => k);
            result.Setup(p => p.Unprotect(It.IsAny<byte[]>())).Returns((byte[] k) => k);
            result.Setup(p => p.Unprotect(It.Is<byte[]>(b => b.SequenceEqual(nullBytes)))).Returns((byte[])null);
            result.Setup(p => p.Unprotect(It.Is<byte[]>(b => b.SequenceEqual(throwBytes)))).Throws<InvalidOperationException>();
            result.Setup(p => p.CreateProtector(It.IsAny<string>())).Returns(result.Object);
            return result;
        }

        private class ProviderStub : BaseSessionAffinityProvider<string>
        {
            public static readonly string KeyNameSetting = "AffinityKeyName";

            public ProviderStub(IDataProtectionProvider dataProtectionProvider, ILogger logger)
                : base(dataProtectionProvider, logger)
            {}

            public override string Mode => "Stub";

            public string LastSetEncryptedKey { get; private set; }

            public void DirectlySetExtractedKeyOnContext(HttpContext context, string key)
            {
                context.Items[AffinityKeyId] = key;
            }

            protected override string GetDestinationAffinityKey(DestinationInfo destination)
            {
                return destination.DestinationId;
            }

            protected override (string Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options)
            {
                Assert.Equal(Mode, options.Mode);
                var keyName = GetSettingValue(KeyNameSetting, options);
                // HttpContext.Items is used here to store the request affinity key for simplicity.
                // In real world scenario, a provider will extract it from request (e.g. header, cookie, etc.)
                var encryptedKey = context.Items.TryGetValue(keyName, out var requestKey) ? requestKey : null;
                return Unprotect((string)encryptedKey);
            }

            protected override void SetAffinityKey(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options, string unencryptedKey)
            {
                var keyName = GetSettingValue(KeyNameSetting, options);
                var encryptedKey = Protect(unencryptedKey);
                context.Items[keyName] = encryptedKey;
                LastSetEncryptedKey = encryptedKey;
            }
        }
    }
}

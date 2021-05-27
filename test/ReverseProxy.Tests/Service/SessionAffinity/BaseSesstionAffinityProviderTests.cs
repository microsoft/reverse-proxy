// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.SessionAffinity
{
    public class BaseSesstionAffinityProviderTests
    {
        private const string InvalidKeyNull = "!invalid key - null!";
        private const string InvalidKeyThrow = "!invalid key - throw!";
        private const string KeyName = "StubAffinityKey";
        private readonly SessionAffinityConfig _defaultOptions = new SessionAffinityConfig
        {
            Enabled = true,
            Mode = "Stub",
            FailurePolicy = "Return503",
            AffinityKeyName = "StubAffinityKey"
        };

        [Theory]
        [MemberData(nameof(FindAffinitizedDestinationsCases))]
        public void Request_FindAffinitizedDestinations(
            HttpContext context,
            DestinationState[] allDestinations,
            AffinityStatus expectedStatus,
            DestinationState expectedDestination,
            byte[] expectedEncryptedKey,
            bool unprotectCalled,
            LogLevel? expectedLogLevel,
            EventId expectedEventId)
        {
            var dataProtector = GetDataProtector();
            var logger = AffinityTestHelper.GetLogger<BaseSessionAffinityProvider<string>>();
            var provider = new ProviderStub(dataProtector.Object, logger.Object);
            var affinityResult = provider.FindAffinitizedDestinations(context, allDestinations, "cluster-1", _defaultOptions);

            if(unprotectCalled)
            {
                dataProtector.Verify(p => p.Unprotect(It.Is<byte[]>(b => b.SequenceEqual(expectedEncryptedKey))), Times.Once);
            }

            Assert.Equal(expectedStatus, affinityResult.Status);
            Assert.Same(expectedDestination, affinityResult.Destinations?.FirstOrDefault());

            if (expectedLogLevel != null)
            {
                logger.Verify(
                    l => l.Log(expectedLogLevel.Value, expectedEventId, It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                    Times.Once);
            }

            if (expectedDestination != null)
            {
                Assert.Equal(1, affinityResult.Destinations.Count);
            }
            else
            {
                Assert.Null(affinityResult.Destinations);
            }
        }

        [Fact]
        public void FindAffinitizedDestination_AffinityDisabledOnCluster_ReturnsAffinityDisabled()
        {
            var provider = new ProviderStub(GetDataProtector().Object, AffinityTestHelper.GetLogger<BaseSessionAffinityProvider<string>>().Object);
            var options = new SessionAffinityConfig
            {
                Enabled = false,
                Mode = _defaultOptions.Mode,
                FailurePolicy = _defaultOptions.FailurePolicy,
                AffinityKeyName = _defaultOptions.AffinityKeyName
            };
            Assert.Throws<InvalidOperationException>(() => provider.FindAffinitizedDestinations(new DefaultHttpContext(), new[] { new DestinationState("1") }, "cluster-1", options));
        }

        [Fact]
        public void AffinitizeRequest_AffinityDisabled_DoNothing()
        {
            var dataProtector = GetDataProtector();
            var provider = new ProviderStub(dataProtector.Object, AffinityTestHelper.GetLogger<BaseSessionAffinityProvider<string>>().Object);
            Assert.Throws<InvalidOperationException>(() => provider.AffinitizeRequest(new DefaultHttpContext(), new SessionAffinityConfig(), new DestinationState("id")));
        }

        [Fact]
        public void AffinitizeRequest_RequestIsAffinitized_DoNothing()
        {
            var dataProtector = GetDataProtector();
            var provider = new ProviderStub(dataProtector.Object, AffinityTestHelper.GetLogger<BaseSessionAffinityProvider<string>>().Object);
            var context = new DefaultHttpContext();
            provider.DirectlySetExtractedKeyOnContext(context, "ExtractedKey");
            provider.AffinitizeRequest(context, _defaultOptions, new DestinationState("id"));
            Assert.Null(provider.LastSetEncryptedKey);
            dataProtector.Verify(p => p.Protect(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void AffinitizeRequest_RequestIsNotAffinitized_SetAffinityKey()
        {
            var dataProtector = GetDataProtector();
            var provider = new ProviderStub(dataProtector.Object, AffinityTestHelper.GetLogger<BaseSessionAffinityProvider<string>>().Object);
            var destination = new DestinationState("dest-A");
            provider.AffinitizeRequest(new DefaultHttpContext(), _defaultOptions, destination);
            Assert.Equal("ZGVzdC1B", provider.LastSetEncryptedKey);
            var keyBytes = Encoding.UTF8.GetBytes(destination.DestinationId);
            dataProtector.Verify(p => p.Protect(It.Is<byte[]>(b => b.SequenceEqual(keyBytes))), Times.Once);
        }

        [Fact]
        public void Ctor_MandatoryArgumentIsNull_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ProviderStub(null, new Mock<ILogger>().Object));
            // CreateDataProtector will return null
            Assert.Throws<ArgumentNullException>(() => new ProviderStub(new Mock<IDataProtector>().Object, new Mock<ILogger>().Object));
            Assert.Throws<ArgumentNullException>(() => new ProviderStub(GetDataProtector().Object, null));
        }

        public static IEnumerable<object[]> FindAffinitizedDestinationsCases()
        {
            var destinations = new[] { new DestinationState("dest-A"), new DestinationState("dest-B"), new DestinationState("dest-C") };
            yield return new object[] { GetHttpContext(new[] { ("SomeKey", "SomeValue") }), destinations, AffinityStatus.AffinityKeyNotSet, null, null, false, null, null };
            yield return new object[] { GetHttpContext(new[] { (KeyName, "dest-B") }), destinations, AffinityStatus.OK, destinations[1], Encoding.UTF8.GetBytes("dest-B"), true, null, null };
            yield return new object[] { GetHttpContext(new[] { (KeyName, "dest-Z") }), destinations, AffinityStatus.DestinationNotFound, null, Encoding.UTF8.GetBytes("dest-Z"), true, LogLevel.Warning, EventIds.DestinationMatchingToAffinityKeyNotFound };
            yield return new object[] { GetHttpContext(new[] { (KeyName, "dest-B") }), new DestinationState[0], AffinityStatus.DestinationNotFound, null, Encoding.UTF8.GetBytes("dest-B"), true, LogLevel.Warning, EventIds.AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster };
            yield return new object[] { GetHttpContext(new[] { (KeyName, "/////") }, false), destinations, AffinityStatus.AffinityKeyExtractionFailed, null, Encoding.UTF8.GetBytes(InvalidKeyNull), false, LogLevel.Error, EventIds.RequestAffinityKeyDecryptionFailed };
            yield return new object[] { GetHttpContext(new[] { (KeyName, InvalidKeyNull) }), destinations, AffinityStatus.AffinityKeyExtractionFailed, null, Encoding.UTF8.GetBytes(InvalidKeyNull), true, LogLevel.Error, EventIds.RequestAffinityKeyDecryptionFailed };
            yield return new object[] { GetHttpContext(new[] { (KeyName, InvalidKeyThrow) }), destinations, AffinityStatus.AffinityKeyExtractionFailed, null, Encoding.UTF8.GetBytes(InvalidKeyThrow), true, LogLevel.Error, EventIds.RequestAffinityKeyDecryptionFailed };
        }

        private static HttpContext GetHttpContext((string Key, string Value)[] items, bool encodeToBase64 = true)
        {
            var context = new DefaultHttpContext
            {
                Items = items.ToDictionary(i => (object)i.Key, i => encodeToBase64 ? (object)Convert.ToBase64String(Encoding.UTF8.GetBytes(i.Value)) : i.Value)
            };
            return context;
        }

        private Mock<IDataProtector> GetDataProtector()
        {
            var result = new Mock<IDataProtector>();
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

            protected override string GetDestinationAffinityKey(DestinationState destination)
            {
                return destination.DestinationId;
            }

            protected override (string Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, SessionAffinityConfig options)
            {
                Assert.Equal(Mode, options.Mode);
                // HttpContext.Items is used here to store the request affinity key for simplicity.
                // In real world scenario, a provider will extract it from request (e.g. header, cookie, etc.)
                var encryptedKey = context.Items.TryGetValue(options.AffinityKeyName, out var requestKey) ? requestKey : null;
                return Unprotect((string)encryptedKey);
            }

            protected override void SetAffinityKey(HttpContext context, SessionAffinityConfig options, string unencryptedKey)
            {
                var encryptedKey = Protect(unencryptedKey);
                context.Items[options.AffinityKeyName] = encryptedKey;
                LastSetEncryptedKey = encryptedKey;
            }
        }
    }
}

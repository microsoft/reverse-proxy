// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Core.Service.Proxy.Infra;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Proxy.Tests
{
    public class ProxyHttpClientFactoryTests : TestAutoMockBase
    {
        [Fact]
        public void Constructor_Works()
        {
            new ProxyHttpClientFactory();
        }

        [Fact]
        public void CreateNormalClient_Works()
        {
            // Arrange
            var factory = new ProxyHttpClientFactory();

            // Act
            var actual1 = factory.CreateNormalClient();
            var actual2 = factory.CreateNormalClient();

            // Assert
            Assert.NotNull(actual1);
            Assert.NotNull(actual2);
            Assert.NotSame(actual2, actual1);
        }

        [Fact]
        public void CreateUpgradableClient_Works()
        {
            // Arrange
            var factory = new ProxyHttpClientFactory();

            // Act
            var actual1 = factory.CreateUpgradableClient();
            var actual2 = factory.CreateUpgradableClient();

            // Assert
            Assert.NotNull(actual1);
            Assert.NotNull(actual2);
            Assert.NotSame(actual2, actual1);
        }
    }
}

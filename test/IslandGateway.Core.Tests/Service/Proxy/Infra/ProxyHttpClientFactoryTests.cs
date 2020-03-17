// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using IslandGateway.Core.Service.Proxy.Infra;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.Service.Proxy.Tests
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
            actual1.Should().NotBeNull();
            actual2.Should().NotBeNull();
            actual1.Should().NotBeSameAs(actual2);
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
            actual1.Should().NotBeNull();
            actual2.Should().NotBeNull();
            actual1.Should().NotBeSameAs(actual2);
        }
    }
}

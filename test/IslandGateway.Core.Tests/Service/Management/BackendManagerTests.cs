// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using IslandGateway.Core.Service.Proxy.Infra;
using Moq;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.Service.Management.Tests
{
    /// <summary>
    /// Tests for the <see cref="BackendManager"/> class.
    /// Additional scenarios are covered in <see cref="ItemManagerBaseTests"/>.
    /// </summary>
    public class BackendManagerTests : TestAutoMockBase
    {
        [Fact]
        public void Constructor_Works()
        {
            Create<BackendManager>();
        }

        [Fact]
        public void GetOrCreateItem_NonExistentItem_CreatesNewItem()
        {
            // Arrange
            var endpointManager = new EndpointManager();
            var proxyHttpClientFactory = new Mock<IProxyHttpClientFactory>().Object;
            Mock<IEndpointManagerFactory>()
                .Setup(e => e.CreateEndpointManager())
                .Returns(endpointManager);
            Mock<IProxyHttpClientFactoryFactory>()
                .Setup(e => e.CreateFactory())
                .Returns(proxyHttpClientFactory);
            var manager = Create<BackendManager>();

            // Act
            var item = manager.GetOrCreateItem("abc", item => { });

            // Assert
            item.Should().NotBeNull();
            item.BackendId.Should().Be("abc");
            item.EndpointManager.Should().BeSameAs(endpointManager);
            item.ProxyHttpClientFactory.Should().BeSameAs(proxyHttpClientFactory);
        }
    }
}

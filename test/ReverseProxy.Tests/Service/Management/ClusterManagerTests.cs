// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Management.Tests
{
    /// <summary>
    /// Tests for the <see cref="ClusterManager"/> class.
    /// Additional scenarios are covered in <see cref="ItemManagerBaseTests"/>.
    /// </summary>
    public class ClusterManagerTests : TestAutoMockBase
    {
        [Fact]
        public void Constructor_Works()
        {
            Create<ClusterManager>();
        }

        [Fact]
        public void GetOrCreateItem_NonExistentItem_CreatesNewItem()
        {
            // Arrange
            var endpointManager = new DestinationManager();
            var proxyHttpClientFactory = new Mock<IProxyHttpClientFactory>().Object;
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(endpointManager);
            var manager = Create<ClusterManager>();

            // Act
            var item = manager.GetOrCreateItem("abc", item => { });

            // Assert
            Assert.NotNull(item);
            Assert.Equal("abc", item.ClusterId);
            Assert.Same(endpointManager, item.DestinationManager);
        }
    }
}

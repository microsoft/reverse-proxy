// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.ConfigModel;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Management.Tests
{
    public class ReverseProxyConfigManagerTests : TestAutoMockBase
    {
        private readonly IClusterManager _clusterManager;
        private readonly IRouteManager _routeManager;

        public ReverseProxyConfigManagerTests()
        {
            var httpClientFactoryMock = new Mock<IProxyHttpClientFactory>(MockBehavior.Strict);
            Mock<IProxyHttpClientFactoryFactory>()
                .Setup(p => p.CreateFactory())
                .Returns(httpClientFactoryMock.Object);

            // The following classes simply store information and using the actual implementations
            // is easier than replicating functionality with mocks.
            Provide<IDestinationManagerFactory, DestinationManagerFactory>();
            _clusterManager = Provide<IClusterManager, ClusterManager>();
            _routeManager = Provide<IRouteManager, RouteManager>();
            Provide<IRuntimeRouteBuilder, RuntimeRouteBuilder>();
        }

        [Fact]
        public void Constructor_Works()
        {
            Create<ReverseProxyConfigManager>();
        }

        [Fact]
        public async Task ApplyConfigurationsAsync_OneClusterOneDestinationOneRoute_Works()
        {
            // Arrange
            const string TestAddress = "https://localhost:123/";

            var cluster = new Cluster
            {
                Destinations = {
                    { "d1", new Destination { Address = TestAddress } }
                }
            };
            var route = new ParsedRoute
            {
                RouteId = "route1",
                ClusterId = "cluster1",
            };

            var dynamicConfigRoot = new DynamicConfigRoot
            {
                Clusters = new Dictionary<string, Cluster> { { "cluster1", cluster }  },
                Routes = new[] { route },
            };
            Mock<IDynamicConfigBuilder>()
                .Setup(d => d.BuildConfigAsync(It.IsAny<IList<ProxyRoute>>(), It.IsAny<IDictionary<string, Cluster>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dynamicConfigRoot);

            var proxyManager = Create<ReverseProxyConfigManager>();

            // Act
            await proxyManager.ApplyConfigurationsAsync(null, null, CancellationToken.None);

            // Assert

            var actualClusters = _clusterManager.GetItems();
            Assert.Single(actualClusters);
            Assert.Equal("cluster1", actualClusters[0].ClusterId);
            Assert.NotNull(actualClusters[0].DestinationManager);
            Assert.NotNull(actualClusters[0].Config.Value);

            var actualDestinations = actualClusters[0].DestinationManager.GetItems();
            Assert.Single(actualDestinations);
            Assert.Equal("d1", actualDestinations[0].DestinationId);
            Assert.NotNull(actualDestinations[0].Config);
            Assert.Equal(TestAddress, actualDestinations[0].Config.Address);

            var actualRoutes = _routeManager.GetItems();
            Assert.Single(actualRoutes);
            Assert.Equal("route1", actualRoutes[0].RouteId);
            Assert.NotNull(actualRoutes[0].Config.Value);
            Assert.Same(actualClusters[0], actualRoutes[0].Config.Value.Cluster);
        }
    }
}

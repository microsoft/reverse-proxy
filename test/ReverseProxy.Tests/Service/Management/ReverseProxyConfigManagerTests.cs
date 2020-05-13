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
        private readonly IBackendManager _backendManager;
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
            _backendManager = Provide<IBackendManager, BackendManager>();
            _routeManager = Provide<IRouteManager, RouteManager>();
            Provide<IRuntimeRouteBuilder, RuntimeRouteBuilder>();
        }

        [Fact]
        public void Constructor_Works()
        {
            Create<ReverseProxyConfigManager>();
        }

        [Fact]
        public async Task ApplyConfigurationsAsync_OneBackendOneDestinationOneRoute_Works()
        {
            // Arrange
            const string TestAddress = "https://localhost:123/";

            var backend = new Backend
            {
                Destinations = {
                    { "d1", new Destination { Address = TestAddress } }
                }
            };
            var route = new ParsedRoute
            {
                RouteId = "route1",
                BackendId = "backend1",
            };

            var dynamicConfigRoot = new DynamicConfigRoot
            {
                Backends = new Dictionary<string, Backend> { { "backend1", backend }  },
                Routes = new[] { route },
            };
            Mock<IDynamicConfigBuilder>()
                .Setup(d => d.BuildConfigAsync(It.IsAny<IConfigErrorReporter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success(dynamicConfigRoot));

            var errorReporter = new TestConfigErrorReporter();
            var proxyManager = Create<ReverseProxyConfigManager>();

            // Act
            var result = await proxyManager.ApplyConfigurationsAsync(errorReporter, CancellationToken.None);

            // Assert
            Assert.True(result);

            var actualBackends = _backendManager.GetItems();
            Assert.Single(actualBackends);
            Assert.Equal("backend1", actualBackends[0].BackendId);
            Assert.NotNull(actualBackends[0].DestinationManager);
            Assert.NotNull(actualBackends[0].Config.Value);

            var actualDestinations = actualBackends[0].DestinationManager.GetItems();
            Assert.Single(actualDestinations);
            Assert.Equal("d1", actualDestinations[0].DestinationId);
            Assert.NotNull(actualDestinations[0].Config.Value);
            Assert.Equal(TestAddress, actualDestinations[0].Config.Value.Address);

            var actualRoutes = _routeManager.GetItems();
            Assert.Single(actualRoutes);
            Assert.Equal("route1", actualRoutes[0].RouteId);
            Assert.NotNull(actualRoutes[0].Config.Value);
            Assert.Same(actualBackends[0], actualRoutes[0].Config.Value.BackendOrNull);
        }
    }
}

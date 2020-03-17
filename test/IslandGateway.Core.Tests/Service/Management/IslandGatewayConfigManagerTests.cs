// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IslandGateway.Core.Abstractions;
using IslandGateway.Core.ConfigModel;
using IslandGateway.Core.Service.Proxy.Infra;
using Moq;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.Service.Management.Tests
{
    public class IslandGatewayConfigManagerTests : TestAutoMockBase
    {
        private readonly IBackendManager _backendManager;
        private readonly IRouteManager _routeManager;

        public IslandGatewayConfigManagerTests()
        {
            var httpClientFactoryMock = new Mock<IProxyHttpClientFactory>(MockBehavior.Strict);
            Mock<IProxyHttpClientFactoryFactory>()
                .Setup(p => p.CreateFactory())
                .Returns(httpClientFactoryMock.Object);

            // The following classes simply store information and using the actual implementations
            // is easier than replicating functionality with mocks.
            Provide<IEndpointManagerFactory, EndpointManagerFactory>();
            _backendManager = Provide<IBackendManager, BackendManager>();
            _routeManager = Provide<IRouteManager, RouteManager>();
            Provide<IRuntimeRouteBuilder, RuntimeRouteBuilder>();
        }

        [Fact]
        public void Constructor_Works()
        {
            Create<IslandGatewayConfigManager>();
        }

        [Fact]
        public async Task ApplyConfigurationsAsync_OneBackendOneEndpointOneRoute_Works()
        {
            // Arrange
            const string TestAddress = "https://localhost:123/";

            var backend = new Backend { BackendId = "backend1" };
            var endpoints = new[] { new BackendEndpoint { EndpointId = "ep1", Address = TestAddress } };
            var route = new ParsedRoute
            {
                RouteId = "route1",
                BackendId = "backend1",
            };

            var dynamicConfigRoot = new DynamicConfigRoot
            {
                Backends = new[] { new BackendWithEndpoints(backend, endpoints) },
                Routes = new[] { route },
            };
            Mock<IDynamicConfigBuilder>()
                .Setup(d => d.BuildConfigAsync(It.IsAny<IConfigErrorReporter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success(dynamicConfigRoot));

            var errorReporter = new TestConfigErrorReporter();
            var gatewayManager = Create<IslandGatewayConfigManager>();

            // Act
            var result = await gatewayManager.ApplyConfigurationsAsync(errorReporter, CancellationToken.None);

            // Assert
            result.Should().BeTrue();

            var actualBackends = _backendManager.GetItems();
            actualBackends.Should().HaveCount(1);
            actualBackends[0].BackendId.Should().Be("backend1");
            actualBackends[0].EndpointManager.Should().NotBeNull();
            actualBackends[0].Config.Value.Should().NotBeNull();

            var actualEndpoints = actualBackends[0].EndpointManager.GetItems();
            actualEndpoints.Should().HaveCount(1);
            actualEndpoints[0].EndpointId.Should().Be("ep1");
            actualEndpoints[0].Config.Value.Should().NotBeNull();
            actualEndpoints[0].Config.Value.Address.Should().Be(TestAddress);

            var actualRoutes = _routeManager.GetItems();
            actualRoutes.Should().HaveCount(1);
            actualRoutes[0].RouteId.Should().Be("route1");
            actualRoutes[0].Config.Value.Should().NotBeNull();
            actualRoutes[0].Config.Value.BackendOrNull.Should().BeSameAs(actualBackends[0]);
        }
    }
}

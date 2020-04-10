// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Core.ConfigModel;
using Microsoft.ReverseProxy.Core.Service.Proxy.Infra;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Management.Tests
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
            Provide<IEndpointManagerFactory, EndpointManagerFactory>();
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
        public async Task ApplyConfigurationsAsync_OneBackendOneEndpointOneRoute_Works()
        {
            // Arrange
            const string TestAddress = "https://localhost:123/";

            var backend = new Backend
            {
                Endpoints = {
                    { "ep1", new BackendEndpoint { Address = TestAddress } }
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

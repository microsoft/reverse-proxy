// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IslandGateway.Core.Abstractions;
using IslandGateway.Core.ConfigModel;
using Moq;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.Service.Tests
{
    public class DynamicConfigBuilderTests : TestAutoMockBase
    {
        [Fact]
        public void Constructor_Works()
        {
            Create<DynamicConfigBuilder>();
        }

        [Fact]
        public async Task BuildConfigAsync_NullInput_Works()
        {
            // Arrange
            var errorReporter = new TestConfigErrorReporter();

            // Act
            var configManager = Create<DynamicConfigBuilder>();
            var result = await configManager.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            errorReporter.Errors.Should().BeEmpty();
            result.Value.Should().NotBeNull();
            result.Value.Backends.Should().BeEmpty();
            result.Value.Routes.Should().BeEmpty();
        }

        [Fact]
        public async Task BuildConfigAsync_EmptyInput_Works()
        {
            // Arrange
            var errorReporter = new TestConfigErrorReporter();
            Mock<IBackendsRepo>()
                .Setup(r => r.GetBackendsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Backend>());

            Mock<IRoutesRepo>()
                .Setup(r => r.GetRoutesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GatewayRoute>());

            // Act
            var configManager = Create<DynamicConfigBuilder>();
            var result = await configManager.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            errorReporter.Errors.Should().BeEmpty();
            result.Value.Should().NotBeNull();
            result.Value.Backends.Should().BeEmpty();
            result.Value.Routes.Should().BeEmpty();
        }

        [Fact]
        public async Task BuildConfigAsync_OneBackend_Works()
        {
            // Arrange
            const string TestAddress = "https://localhost:123/";

            var errorReporter = new TestConfigErrorReporter();
            Mock<IBackendsRepo>()
                .Setup(r => r.GetBackendsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Backend[] { new Backend { BackendId = "backend1" } });

            Mock<IBackendEndpointsRepo>()
                .Setup(r => r.GetEndpointsAsync("backend1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new BackendEndpoint { EndpointId = "ep1", Address = TestAddress } });

            Mock<IRoutesRepo>()
                .Setup(r => r.GetRoutesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GatewayRoute>());

            // Act
            var configManager = Create<DynamicConfigBuilder>();
            var result = await configManager.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            errorReporter.Errors.Should().BeEmpty();
            result.Value.Should().NotBeNull();
            result.Value.Backends.Should().HaveCount(1);
            result.Value.Backends[0].Backend.BackendId.Should().Be("backend1");
            result.Value.Backends[0].Endpoints.Should().HaveCount(1);
            result.Value.Backends[0].Endpoints[0].EndpointId.Should().Be("ep1");
            result.Value.Backends[0].Endpoints[0].Address.Should().Be(TestAddress);
        }

        [Fact]
        public async Task BuildConfigAsync_ValidRoute_Works()
        {
            // Arrange
            var errorReporter = new TestConfigErrorReporter();
            Mock<IBackendsRepo>()
                .Setup(r => r.GetBackendsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Backend>());

            var route1 = new GatewayRoute { RouteId = "route1", Rule = "Host('example.com')", Priority = 1, BackendId = "backend1" };
            Mock<IRoutesRepo>()
                .Setup(r => r.GetRoutesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { route1 });

            var parsedRoute1 = new ParsedRoute();
            Mock<IRouteParser>()
                .Setup(r => r.ParseRoute(route1, errorReporter))
                .Returns(Result.Success(parsedRoute1));

            Mock<IRouteValidator>()
                .Setup(r => r.ValidateRoute(parsedRoute1, errorReporter))
                .Returns(true);

            // Act
            var configManager = Create<DynamicConfigBuilder>();
            var result = await configManager.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            errorReporter.Errors.Should().BeEmpty();
            result.Value.Should().NotBeNull();
            result.Value.Backends.Should().BeEmpty();
            result.Value.Routes.Should().HaveCount(1);
            result.Value.Routes[0].Should().BeSameAs(parsedRoute1);
        }

        [Fact]
        public async Task BuildConfigAsync_RouteParseError_SkipsRoute()
        {
            // Arrange
            var errorReporter = new TestConfigErrorReporter();
            Mock<IBackendsRepo>()
                .Setup(r => r.GetBackendsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Backend>());

            var route1 = new GatewayRoute { RouteId = "route1", Rule = "Host('example.com')", Priority = 1, BackendId = "backend1" };
            Mock<IRoutesRepo>()
                .Setup(r => r.GetRoutesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { route1 });

            var parsedRoute1 = new ParsedRoute();
            Mock<IRouteParser>()
                .Setup(r => r.ParseRoute(route1, errorReporter))
                .Returns(Result.Failure<ParsedRoute>());

            // Act
            var configManager = Create<DynamicConfigBuilder>();
            var result = await configManager.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Backends.Should().BeEmpty();
            result.Value.Routes.Should().BeEmpty();
        }

        [Fact]
        public async Task BuildConfigAsync_RouteValidationError_SkipsRoute()
        {
            // Arrange
            var errorReporter = new TestConfigErrorReporter();
            Mock<IBackendsRepo>()
                .Setup(r => r.GetBackendsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Backend>());

            var route1 = new GatewayRoute { RouteId = "route1", Rule = "Host('example.com')", Priority = 1, BackendId = "backend1" };
            Mock<IRoutesRepo>()
                .Setup(r => r.GetRoutesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { route1 });

            var parsedRoute1 = new ParsedRoute();
            Mock<IRouteParser>()
                .Setup(r => r.ParseRoute(route1, errorReporter))
                .Returns(Result.Success(parsedRoute1));

            Mock<IRouteValidator>()
                .Setup(r => r.ValidateRoute(parsedRoute1, errorReporter))
                .Returns(false);

            // Act
            var configManager = Create<DynamicConfigBuilder>();
            var result = await configManager.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Backends.Should().BeEmpty();
            result.Value.Routes.Should().BeEmpty();
        }
    }
}

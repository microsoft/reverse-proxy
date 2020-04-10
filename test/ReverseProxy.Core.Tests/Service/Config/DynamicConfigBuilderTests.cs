// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Core.ConfigModel;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Tests
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
                .ReturnsAsync(new Dictionary<string, Backend>());

            Mock<IRoutesRepo>()
                .Setup(r => r.GetRoutesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProxyRoute>());

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
                .ReturnsAsync(new Dictionary<string, Backend>
                {
                    {
                        "backend1", new Backend
                        {
                            Endpoints =
                            {
                                { "ep1", new BackendEndpoint { Address = TestAddress } }
                            }
                        }
                    }
                });

            Mock<IRoutesRepo>()
                .Setup(r => r.GetRoutesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProxyRoute>());

            // Act
            var configManager = Create<DynamicConfigBuilder>();
            var result = await configManager.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            errorReporter.Errors.Should().BeEmpty();
            result.Value.Should().NotBeNull();
            result.Value.Backends.Should().HaveCount(1);
            var backend = result.Value.Backends["backend1"];
            backend.Should().NotBeNull();
            backend.Endpoints.Should().HaveCount(1);
            var endpoint = backend.Endpoints["ep1"];
            endpoint.Should().NotBeNull();
            endpoint.Address.Should().Be(TestAddress);
        }

        [Fact]
        public async Task BuildConfigAsync_ValidRoute_Works()
        {
            // Arrange
            var errorReporter = new TestConfigErrorReporter();
            Mock<IBackendsRepo>()
                .Setup(r => r.GetBackendsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, Backend>());

            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, BackendId = "backend1" };
            Mock<IRoutesRepo>()
                .Setup(r => r.GetRoutesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { route1 });

            Mock<IRouteValidator>()
                .Setup(r => r.ValidateRoute(It.IsAny<ParsedRoute>(), errorReporter))
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
            result.Value.Routes[0].RouteId.Should().BeSameAs(route1.RouteId);
        }

        [Fact]
        public async Task BuildConfigAsync_RouteParseError_SkipsRoute()
        {
            // Arrange
            var errorReporter = new TestConfigErrorReporter();
            Mock<IBackendsRepo>()
                .Setup(r => r.GetBackendsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, Backend>());

            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, BackendId = "backend1" };
            Mock<IRoutesRepo>()
                .Setup(r => r.GetRoutesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { route1 });

            var parsedRoute1 = new ParsedRoute();

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
                .ReturnsAsync(new Dictionary<string, Backend>());

            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, BackendId = "backend1" };
            Mock<IRoutesRepo>()
                .Setup(r => r.GetRoutesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { route1 });

            var parsedRoute1 = new ParsedRoute();

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

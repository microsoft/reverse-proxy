// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using FluentAssertions;
using Microsoft.ReverseProxy.Core.ConfigModel;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Core.Service.Management;
using Microsoft.ReverseProxy.Core.Service.Proxy.Infra;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Tests
{
    public class RuntimeRouteBuilderTests : TestAutoMockBase
    {
        [Fact]
        public void Constructor_Works()
        {
            Create<RuntimeRouteBuilder>();
        }

        [Fact]
        public void BuildEndpoints_HostAndPath_Works()
        {
            // Arrange
            var builder = Create<RuntimeRouteBuilder>();
            var parsedRoute = new ParsedRoute
            {
                RouteId = "route1",
                Host = "example.com",
                Path = "/a",
                Priority = 12,
            };
            var backend = new BackendInfo("backend1", new EndpointManager(), new Mock<IProxyHttpClientFactory>().Object);
            var routeInfo = new RouteInfo("route1");

            // Act
            var config = builder.Build(parsedRoute, backend, routeInfo);

            // Assert
            config.BackendOrNull.Should().BeSameAs(backend);
            config.Priority.Should().Be(12);
            config.MatcherSummary.Should().Be(parsedRoute.GetMatcherSummary());
            config.AspNetCoreEndpoints.Should().HaveCount(1);
            var routeEndpoint = config.AspNetCoreEndpoints[0] as AspNetCore.Routing.RouteEndpoint;
            routeEndpoint.DisplayName.Should().Be("route1");
            routeEndpoint.Metadata.GetMetadata<RouteConfig>().Should().BeSameAs(config);
            routeEndpoint.RoutePattern.RawText.Should().Be("/a");

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<AspNetCore.Routing.HostAttribute>();
            hostMetadata.Should().NotBeNull();
            hostMetadata.Hosts.Should().BeEquivalentTo("example.com");
        }

        [Fact]
        public void BuildEndpoints_JustHost_Works()
        {
            // Arrange
            var builder = Create<RuntimeRouteBuilder>();
            var parsedRoute = new ParsedRoute
            {
                RouteId = "route1",
                Host = "example.com",
                Priority = 12,
            };
            var backend = new BackendInfo("backend1", new EndpointManager(), new Mock<IProxyHttpClientFactory>().Object);
            var routeInfo = new RouteInfo("route1");

            // Act
            var config = builder.Build(parsedRoute, backend, routeInfo);

            // Assert
            config.BackendOrNull.Should().BeSameAs(backend);
            config.Priority.Should().Be(12);
            config.MatcherSummary.Should().Be(parsedRoute.GetMatcherSummary());
            config.AspNetCoreEndpoints.Should().HaveCount(1);
            var routeEndpoint = config.AspNetCoreEndpoints[0] as AspNetCore.Routing.RouteEndpoint;
            routeEndpoint.DisplayName.Should().Be("route1");
            routeEndpoint.Metadata.GetMetadata<RouteConfig>().Should().BeSameAs(config);
            routeEndpoint.RoutePattern.RawText.Should().Be("/{**catchall}");

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<AspNetCore.Routing.HostAttribute>();
            hostMetadata.Should().NotBeNull();
            hostMetadata.Hosts.Should().BeEquivalentTo("example.com");
        }

        [Fact]
        public void BuildEndpoints_JustHostWithWildcard_Works()
        {
            // Arrange
            var builder = Create<RuntimeRouteBuilder>();
            var parsedRoute = new ParsedRoute
            {
                RouteId = "route1",
                Host = "*.example.com",
                Priority = 12,
            };
            var backend = new BackendInfo("backend1", new EndpointManager(), new Mock<IProxyHttpClientFactory>().Object);
            var routeInfo = new RouteInfo("route1");

            // Act
            var config = builder.Build(parsedRoute, backend, routeInfo);

            // Assert
            config.BackendOrNull.Should().BeSameAs(backend);
            config.Priority.Should().Be(12);
            config.MatcherSummary.Should().Be(parsedRoute.GetMatcherSummary());
            config.AspNetCoreEndpoints.Should().HaveCount(1);
            var routeEndpoint = config.AspNetCoreEndpoints[0] as AspNetCore.Routing.RouteEndpoint;
            routeEndpoint.DisplayName.Should().Be("route1");
            routeEndpoint.Metadata.GetMetadata<RouteConfig>().Should().BeSameAs(config);
            routeEndpoint.RoutePattern.RawText.Should().Be("/{**catchall}");

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<AspNetCore.Routing.HostAttribute>();
            hostMetadata.Should().NotBeNull();
            hostMetadata.Hosts.Should().BeEquivalentTo("*.example.com");
        }

        [Fact]
        public void BuildEndpoints_JustPath_Works()
        {
            // Arrange
            var builder = Create<RuntimeRouteBuilder>();
            var parsedRoute = new ParsedRoute
            {
                RouteId = "route1",
                Path = "/a",
                Priority = 12,
            };
            var backend = new BackendInfo("backend1", new EndpointManager(), new Mock<IProxyHttpClientFactory>().Object);
            var routeInfo = new RouteInfo("route1");

            // Act
            var config = builder.Build(parsedRoute, backend, routeInfo);

            // Assert
            config.BackendOrNull.Should().BeSameAs(backend);
            config.Priority.Should().Be(12);
            config.MatcherSummary.Should().Be(parsedRoute.GetMatcherSummary());
            config.AspNetCoreEndpoints.Should().HaveCount(1);
            var routeEndpoint = config.AspNetCoreEndpoints[0] as AspNetCore.Routing.RouteEndpoint;
            routeEndpoint.DisplayName.Should().Be("route1");
            routeEndpoint.Metadata.GetMetadata<RouteConfig>().Should().BeSameAs(config);
            routeEndpoint.RoutePattern.RawText.Should().Be("/a");

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<AspNetCore.Routing.HostAttribute>();
            hostMetadata.Should().BeNull();
        }

        [Fact]
        public void BuildEndpoints_NullMatchers_Works()
        {
            // Arrange
            var builder = Create<RuntimeRouteBuilder>();
            var parsedRoute = new ParsedRoute
            {
                RouteId = "route1",
                Priority = 12,
            };
            var backend = new BackendInfo("backend1", new EndpointManager(), new Mock<IProxyHttpClientFactory>().Object);
            var routeInfo = new RouteInfo("route1");

            // Act
            var config = builder.Build(parsedRoute, backend, routeInfo);

            // Assert
            config.BackendOrNull.Should().BeSameAs(backend);
            config.Priority.Should().Be(12);
            config.MatcherSummary.Should().Be("");
            config.AspNetCoreEndpoints.Should().HaveCount(1);
            var routeEndpoint = config.AspNetCoreEndpoints[0] as AspNetCore.Routing.RouteEndpoint;
            routeEndpoint.DisplayName.Should().Be("route1");
            routeEndpoint.Metadata.GetMetadata<RouteConfig>().Should().BeSameAs(config);
            routeEndpoint.RoutePattern.RawText.Should().Be("/{**catchall}");

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<AspNetCore.Routing.HostAttribute>();
            hostMetadata.Should().BeNull();
        }

        [Fact]
        public void BuildEndpoints_InvalidPath_BubblesOutException()
        {
            // Arrange
            var builder = Create<RuntimeRouteBuilder>();
            var parsedRoute = new ParsedRoute
            {
                RouteId = "route1",
                Path = "/{invalid",
                Priority = 12,
            };
            var backend = new BackendInfo("backend1", new EndpointManager(), new Mock<IProxyHttpClientFactory>().Object);
            var routeInfo = new RouteInfo("route1");

            // Act
            Action action = () => builder.Build(parsedRoute, backend, routeInfo);

            // Assert
            action.Should().ThrowExactly<AspNetCore.Routing.Patterns.RoutePatternException>();
        }
    }
}

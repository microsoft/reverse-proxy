// <copyright file="RuntimeRouteBuilderTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using FluentAssertions;
using IslandGateway.Core.ConfigModel;
using IslandGateway.Core.RuntimeModel;
using IslandGateway.Core.Service.Management;
using IslandGateway.Core.Service.Proxy.Infra;
using Moq;
using Tests.Common;
using Xunit;
using AspNetCore = Microsoft.AspNetCore;

namespace IslandGateway.Core.Service.Tests
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
                Rule = "Host('example.com') && Path('/a')",
                Matchers = new List<RuleMatcherBase>
                {
                    new HostMatcher("Host", new[] { "example.com" }),
                    new PathMatcher("Path", new[] { "/a" }),
                },
                Priority = 12,
            };
            var backend = new BackendInfo("backend1", new EndpointManager(), new Mock<IProxyHttpClientFactory>().Object);
            var routeInfo = new RouteInfo("route1");

            // Act
            var config = builder.Build(parsedRoute, backend, routeInfo);

            // Assert
            config.BackendOrNull.Should().BeSameAs(backend);
            config.Priority.Should().Be(12);
            config.Rule.Should().Be("Host('example.com') && Path('/a')");
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
                Rule = "Host('example.com')",
                Matchers = new List<RuleMatcherBase>
                {
                    new HostMatcher("Host", new[] { "example.com" }),
                },
                Priority = 12,
            };
            var backend = new BackendInfo("backend1", new EndpointManager(), new Mock<IProxyHttpClientFactory>().Object);
            var routeInfo = new RouteInfo("route1");

            // Act
            var config = builder.Build(parsedRoute, backend, routeInfo);

            // Assert
            config.BackendOrNull.Should().BeSameAs(backend);
            config.Priority.Should().Be(12);
            config.Rule.Should().Be("Host('example.com')");
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
                Rule = "Host('*.example.com')",
                Matchers = new List<RuleMatcherBase>
                {
                    new HostMatcher("Host", new[] { "*.example.com" }),
                },
                Priority = 12,
            };
            var backend = new BackendInfo("backend1", new EndpointManager(), new Mock<IProxyHttpClientFactory>().Object);
            var routeInfo = new RouteInfo("route1");

            // Act
            var config = builder.Build(parsedRoute, backend, routeInfo);

            // Assert
            config.BackendOrNull.Should().BeSameAs(backend);
            config.Priority.Should().Be(12);
            config.Rule.Should().Be("Host('*.example.com')");
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
                Rule = "Path('/a')",
                Matchers = new List<RuleMatcherBase>
                {
                    new PathMatcher("Path", new[] { "/a" }),
                },
                Priority = 12,
            };
            var backend = new BackendInfo("backend1", new EndpointManager(), new Mock<IProxyHttpClientFactory>().Object);
            var routeInfo = new RouteInfo("route1");

            // Act
            var config = builder.Build(parsedRoute, backend, routeInfo);

            // Assert
            config.BackendOrNull.Should().BeSameAs(backend);
            config.Priority.Should().Be(12);
            config.Rule.Should().Be("Path('/a')");
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
                Rule = "Host('example.com')",
                Priority = 12,
            };
            var backend = new BackendInfo("backend1", new EndpointManager(), new Mock<IProxyHttpClientFactory>().Object);
            var routeInfo = new RouteInfo("route1");

            // Act
            var config = builder.Build(parsedRoute, backend, routeInfo);

            // Assert
            config.BackendOrNull.Should().BeSameAs(backend);
            config.Priority.Should().Be(12);
            config.Rule.Should().Be("Host('example.com')");
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
                Rule = "Path('/{invalid')",
                Matchers = new List<RuleMatcherBase>
                {
                    new PathMatcher("Path", new[] { "/{invalid" }),
                },
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

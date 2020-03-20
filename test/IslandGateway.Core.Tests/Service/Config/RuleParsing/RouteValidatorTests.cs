// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using IslandGateway.Core.Abstractions;
using IslandGateway.Core.ConfigModel;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.Service.Tests
{
    public class RouteValidatorTests : TestAutoMockBase
    {
        private readonly RouteParser _routeParser;

        public RouteValidatorTests()
        {
            _routeParser = Create<RouteParser>();
        }

        [Fact]
        public void Constructor_Works()
        {
            Create<RouteValidator>();
        }

        [Theory]
        [InlineData("example.com", "/a/", null)]
        [InlineData("example.com", "/a/**", null)]
        [InlineData("example.com", "/a/**", "GET")]
        [InlineData("example.com", null, "get")]
        [InlineData("example.com", null, "gEt,put")]
        [InlineData("example.com", null, "gEt,put,POST,traCE,PATCH,DELETE,Head")]
        [InlineData("*.example.com", null, null)]
        [InlineData("a-b.example.com", null, null)]
        [InlineData("a-b.b-c.example.com", null, null)]
        public void Accepts_ValidRules(string host, string path, string methods)
        {
            // Arrange
            var route = new GatewayRoute {
                RouteId = "route1",
                Host = host,
                Path = path,
                Methods = methods?.Split(","),
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.ErrorReporter.Errors.Should().BeEmpty();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Rejects_MissingRouteId(string routeId)
        {
            // Arrange
            var errorReporter = new TestConfigErrorReporter();
            var parsedRoute = new ParsedRoute { RouteId = routeId };
            var validator = Create<RouteValidator>();

            // Act
            var isSuccess = validator.ValidateRoute(parsedRoute, errorReporter);

            // Assert
            isSuccess.Should().BeFalse();
            errorReporter.Errors.Should().Contain(err => err.ErrorCode == ConfigErrors.ParsedRouteMissingId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("/")]
        public void Rejects_MissingHost(string path)
        {
            // Arrange
            var route = new GatewayRoute
            {
                RouteId = "route1",
                Path = path,
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorReporter.Errors.Should().Contain(err => err.ErrorCode == ConfigErrors.ParsedRouteRuleMissingHostMatcher);
        }

        [Theory]
        [InlineData(".example.com")]
        [InlineData("example*.com")]
        [InlineData("example.*.com")]
        [InlineData("example.*a.com")]
        [InlineData("*example.com")]
        [InlineData("-example.com")]
        [InlineData("example-.com")]
        [InlineData("-example-.com")]
        [InlineData("a.-example.com")]
        [InlineData("a.example-.com")]
        [InlineData("a.-example-.com")]
        public void Rejects_InvalidHost(string host)
        {
            // Arrange
            var route = new GatewayRoute
            {
                RouteId = "route1",
                Host = host,
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorReporter.Errors.Should().Contain(err => err.ErrorCode == ConfigErrors.ParsedRouteRuleInvalidMatcher && err.Message.Contains("Invalid host name"));
        }

        [Theory]
        [InlineData("/{***a}")]
        [InlineData("/{")]
        [InlineData("/}")]
        [InlineData("/{ab/c}")]
        public void Rejects_InvalidPath(string path)
        {
            // Arrange
            var route = new GatewayRoute
            {
                RouteId = "route1",
                Host = "example.com",
                Path = path,
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorReporter.Errors.Should().Contain(err => err.ErrorCode == ConfigErrors.ParsedRouteRuleInvalidMatcher && err.Message.Contains("Invalid path pattern"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("gett")]
        [InlineData("get,post,get")]
        public void Rejects_InvalidMethod(string methods)
        {
            // Arrange
            var route = new GatewayRoute
            {
                RouteId = "route1",
                Host = "example.com",
                Methods = methods.Split(","),
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorReporter.Errors.Should().Contain(err => err.ErrorCode == ConfigErrors.ParsedRouteRuleInvalidMatcher && err.Message.Contains("verb"));
        }

        private (bool IsSuccess, TestConfigErrorReporter ErrorReporter) RunScenario(GatewayRoute route)
        {
            var errorReporter = new TestConfigErrorReporter();
            var parseResult = _routeParser.ParseRoute(route, errorReporter);
            parseResult.IsSuccess.Should().BeTrue();
            var parsedRoute = parseResult.Value;

            var validator = Create<RouteValidator>();
            var isSuccess = validator.ValidateRoute(parsedRoute, errorReporter);
            return (isSuccess, errorReporter);
        }
    }
}

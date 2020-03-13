// <copyright file="RouteValidatorTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

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
            Provide<IRuleParser, RuleParser>();
            _routeParser = Create<RouteParser>();
        }

        [Fact]
        public void Constructor_Works()
        {
            Create<RouteValidator>();
        }

        [Theory]
        [InlineData("Host('example.com') && Path('/a/')")]
        [InlineData("Host('example.com') && Path('/a/**')")]
        [InlineData("Path('/a/**') && Host('example.com')")]
        [InlineData("Path('/a/**') && Host('example.com') && Method('GET')")]
        [InlineData("Host('example.com') && Method('get')")]
        [InlineData("Host('example.com') && Method('gEt', 'put')")]
        [InlineData("Host('example.com') && Method('gEt', 'put', 'POST', 'traCE', 'PATCH', 'DELETE', 'HEAd')")]
        [InlineData("Host('*.example.com')")]
        [InlineData("Host('a-b.example.com')")]
        [InlineData("Host('a-b.b-c.example.com')")]
        public void Accepts_ValidRules(string rule)
        {
            // Arrange
            var route = new GatewayRoute
            {
                RouteId = "route1",
                Rule = rule,
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
            bool isSuccess = validator.ValidateRoute(parsedRoute, errorReporter);

            // Assert
            isSuccess.Should().BeFalse();
            errorReporter.Errors.Should().Contain(err => err.ErrorCode == ConfigErrors.ParsedRouteMissingId);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Method('GET')")]
        public void Rejects_MissingHost(string rule)
        {
            // Arrange
            var route = new GatewayRoute
            {
                RouteId = "route1",
                Rule = rule,
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorReporter.Errors.Should().Contain(err => err.ErrorCode == ConfigErrors.ParsedRouteRuleMissingHostMatcher);
        }

        [Theory]
        [InlineData("Host('.example.com')")]
        [InlineData("Host('example*.com')")]
        [InlineData("Host('example.*.com')")]
        [InlineData("Host('example.*a.com')")]
        [InlineData("Host('*example.com')")]
        [InlineData("Host('-example.com')")]
        [InlineData("Host('example-.com')")]
        [InlineData("Host('-example-.com')")]
        [InlineData("Host('a.-example.com')")]
        [InlineData("Host('a.example-.com')")]
        [InlineData("Host('a.-example-.com')")]
        public void Rejects_InvalidHost(string rule)
        {
            // Arrange
            var route = new GatewayRoute
            {
                RouteId = "route1",
                Rule = rule,
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorReporter.Errors.Should().Contain(err => err.ErrorCode == ConfigErrors.ParsedRouteRuleInvalidMatcher && err.Message.Contains("Invalid host name"));
        }

        [Theory]
        [InlineData("Host('example.com') && Path('/{***a}')")]
        [InlineData("Host('example.com') && Path('/{')")]
        [InlineData("Host('example.com') && Path('/}')")]
        [InlineData("Host('example.com') && Path('/{ab/c}')")]
        public void Rejects_InvalidPath(string rule)
        {
            // Arrange
            var route = new GatewayRoute
            {
                RouteId = "route1",
                Rule = rule,
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorReporter.Errors.Should().Contain(err => err.ErrorCode == ConfigErrors.ParsedRouteRuleInvalidMatcher && err.Message.Contains("Invalid path pattern"));
        }

        [Theory]
        [InlineData("Host('example.com') && Method('')")]
        [InlineData("Host('example.com') && Method('gett')")]
        [InlineData("Host('example.com') && Method('get', 'post', 'get')")]
        public void Rejects_InvalidMethod(string rule)
        {
            // Arrange
            var route = new GatewayRoute
            {
                RouteId = "route1",
                Rule = rule,
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorReporter.Errors.Should().Contain(err => err.ErrorCode == ConfigErrors.ParsedRouteRuleInvalidMatcher && err.Message.Contains("verb"));
        }

        [Fact]
        public void Rejects_MultipleHosts()
        {
            // Arrange
            var route = new GatewayRoute
            {
                RouteId = "route1",
                Rule = "Host('example.com') && Host('example.com')",
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorReporter.Errors.Should().Contain(err => err.ErrorCode == ConfigErrors.ParsedRouteRuleMultipleHostMatchers);
        }

        [Fact]
        public void Rejects_MultiplePaths()
        {
            // Arrange
            var route = new GatewayRoute
            {
                RouteId = "route1",
                Rule = "Path('/a') && Path('/a')",
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorReporter.Errors.Should().Contain(err => err.ErrorCode == ConfigErrors.ParsedRouteRuleMultiplePathMatchers);
        }

        private (bool IsSuccess, TestConfigErrorReporter ErrorReporter) RunScenario(GatewayRoute route)
        {
            var errorReporter = new TestConfigErrorReporter();
            var parseResult = _routeParser.ParseRoute(route, errorReporter);
            parseResult.IsSuccess.Should().BeTrue();
            var parsedRoute = parseResult.Value;

            var validator = Create<RouteValidator>();
            bool isSuccess = validator.ValidateRoute(parsedRoute, errorReporter);
            return (isSuccess, errorReporter);
        }
    }
}

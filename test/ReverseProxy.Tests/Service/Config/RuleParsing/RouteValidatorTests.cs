// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.ConfigModel;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Tests
{
    public class RouteValidatorTests : TestAutoMockBase
    {
        [Fact]
        public void Constructor_Works()
        {
            Create<RouteValidator>();
        }

        [Theory]
        [InlineData("example.com", "/a/", null)]
        [InlineData("example.com", "/a/**", null)]
        [InlineData("example.com", "/a/**", "GET")]
        [InlineData(null, "/a/", null)]
        [InlineData(null, "/a/**", "GET")]
        [InlineData("example.com", null, "get")]
        [InlineData("example.com", null, "gEt,put")]
        [InlineData("example.com", null, "gEt,put,POST,traCE,PATCH,DELETE,Head")]
        [InlineData("*.example.com", null, null)]
        [InlineData("a-b.example.com", null, null)]
        [InlineData("a-b.b-c.example.com", null, null)]
        public void Accepts_ValidRules(string host, string path, string methods)
        {
            // Arrange
            var route = new ParsedRoute
            {
                RouteId = "route1",
                Host = host,
                Path = path,
                Methods = methods?.Split(","),
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Empty(result.ErrorReporter.Errors);
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
            Assert.False(isSuccess);
            Assert.Contains(errorReporter.Errors, err => err.ErrorCode == ConfigErrors.ParsedRouteMissingId);
        }

        [Fact]
        public void Rejects_MissingHostAndPath()
        {
            // Arrange
            var route = new ParsedRoute
            {
                RouteId = "route1",
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(result.ErrorReporter.Errors, err => err.ErrorCode == ConfigErrors.ParsedRouteMissingHostAndPath);
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
            var route = new ParsedRoute
            {
                RouteId = "route1",
                Host = host,
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(result.ErrorReporter.Errors, err => err.ErrorCode == ConfigErrors.ParsedRouteRuleInvalidMatcher && err.Message.Contains("Invalid host name"));
        }

        [Theory]
        [InlineData("/{***a}")]
        [InlineData("/{")]
        [InlineData("/}")]
        [InlineData("/{ab/c}")]
        public void Rejects_InvalidPath(string path)
        {
            // Arrange
            var route = new ParsedRoute
            {
                RouteId = "route1",
                Path = path,
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(result.ErrorReporter.Errors, err => err.ErrorCode == ConfigErrors.ParsedRouteRuleInvalidMatcher && err.Message.Contains("Invalid path pattern"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("gett")]
        [InlineData("get,post,get")]
        public void Rejects_InvalidMethod(string methods)
        {
            // Arrange
            var route = new ParsedRoute
            {
                RouteId = "route1",
                Methods = methods.Split(","),
                BackendId = "be1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(result.ErrorReporter.Errors, err => err.ErrorCode == ConfigErrors.ParsedRouteRuleInvalidMatcher && err.Message.Contains("verb"));
        }

        private (bool IsSuccess, TestConfigErrorReporter ErrorReporter) RunScenario(ParsedRoute parsedRoute)
        {
            var errorReporter = new TestConfigErrorReporter();

            var validator = Create<RouteValidator>();
            var isSuccess = validator.ValidateRoute(parsedRoute, errorReporter);
            return (isSuccess, errorReporter);
        }
    }
}

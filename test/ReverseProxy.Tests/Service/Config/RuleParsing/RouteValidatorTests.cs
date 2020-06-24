// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.ConfigModel;
using Microsoft.ReverseProxy.Service.Config;
using Moq;
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
                ClusterId = "cluster1",
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
                ClusterId = "cluster1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(result.ErrorReporter.Errors, err => err.ErrorCode == ConfigErrors.ParsedRouteRuleHasNoMatchers);
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
                ClusterId = "cluster1",
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
                ClusterId = "cluster1",
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
                ClusterId = "cluster1",
            };

            // Act
            var result = RunScenario(route);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(result.ErrorReporter.Errors, err => err.ErrorCode == ConfigErrors.ParsedRouteRuleInvalidMatcher && err.Message.Contains("verb"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("defaulT")]
        [InlineData("anonyMous")]
        public void Accepts_ReservedAuthorization(string policy)
        {
            var route = new ParsedRoute
            {
                RouteId = "route1",
                Authorization = policy,
                Host = "localhost",
                ClusterId = "cluster1",
            };

            var result = RunScenario(route);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.ErrorReporter.Errors);
        }

        [Fact]
        public void Accepts_CustomAuthorization()
        {
            var authzOptions = new AuthorizationOptions();
            authzOptions.AddPolicy("custom", builder => builder.RequireAuthenticatedUser());
            Provide<IAuthorizationPolicyProvider>(new DefaultAuthorizationPolicyProvider(Options.Create(authzOptions)));
            var route = new ParsedRoute
            {
                RouteId = "route1",
                Authorization = "custom",
                Host = "localhost",
                ClusterId = "cluster1",
            };

            var result = RunScenario(route);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.ErrorReporter.Errors);
        }

        [Fact]
        public void Rejects_UnknownAuthorization()
        {
            var route = new ParsedRoute
            {
                RouteId = "route1",
                Authorization = "unknown",
                ClusterId = "cluster1",
            };

            var result = RunScenario(route);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.ErrorReporter.Errors, err => err.ErrorCode == ConfigErrors.ParsedRouteRuleInvalidAuthorization && err.Message.Contains("Authorization policy 'unknown' not found"));
        }

        private (bool IsSuccess, TestConfigErrorReporter ErrorReporter) RunScenario(ParsedRoute parsedRoute)
        {
            var errorReporter = new TestConfigErrorReporter();

            Mock<ITransformBuilder>().Setup(builder
                => builder.Validate(It.IsAny<IList<IDictionary<string, string>>>(), It.IsAny<string>(), It.IsAny<IConfigErrorReporter>())).Returns(true);
            var validator = Create<RouteValidator>();
            var isSuccess = validator.ValidateRoute(parsedRoute, errorReporter);
            return (isSuccess, errorReporter);
        }
    }
}

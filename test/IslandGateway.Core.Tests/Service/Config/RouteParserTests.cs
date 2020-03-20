// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using FluentAssertions;
using IslandGateway.Core.Abstractions;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.Service.Tests
{
    public class RouteParserTests : TestAutoMockBase
    {
        [Fact]
        public void ParseRoute_ValidRoute_Works()
        {
            // Arrange
            const string TestRouteId = "route1";
            const string TestBackendId = "backend1";
            const string TestHost = "example.com";
            const int TestPriority = 2;

            var matchers = new[] { new HostMatcher(TestHost) };
            var route = new GatewayRoute
            {
                RouteId = TestRouteId,
                Host = TestHost,
                Priority = TestPriority,
                BackendId = TestBackendId,
                Metadata = new Dictionary<string, string>
                {
                    { "key", "value" },
                },
            };
            var routeParser = Create<RouteParser>();
            var errorReporter = new TestConfigErrorReporter();

            // Act
            var result = routeParser.ParseRoute(route, errorReporter);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.RouteId.Should().Be(TestRouteId);
            result.Value.Priority.Should().Be(TestPriority);
            result.Value.Matchers.Should().BeEquivalentTo(matchers);
            result.Value.BackendId.Should().Be(TestBackendId);
            result.Value.Metadata["key"].Should().Be("value");
            errorReporter.Errors.Should().BeEmpty();
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace IslandGateway.Core.Abstractions.Tests
{
    public class GatewayRouteTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new GatewayRoute();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var sut = new GatewayRoute
            {
                RouteId = "route1",
                Rule = "Host('example.com')",
                Priority = 2,
                BackendId = "backend1",
                Metadata = new Dictionary<string, string>
                {
                    { "key", "value" },
                },
            };

            // Act
            var clone = sut.DeepClone();

            // Assert
            clone.Should().NotBeSameAs(sut);
            clone.RouteId.Should().Be(sut.RouteId);
            clone.Rule.Should().Be(sut.Rule);
            clone.Priority.Should().Be(sut.Priority);
            clone.BackendId.Should().Be(sut.BackendId);
            clone.Metadata.Should().NotBeNull();
            clone.Metadata.Should().NotBeSameAs(sut.Metadata);
            clone.Metadata["key"].Should().Be("value");
        }

        [Fact]
        public void DeepClone_Nulls_Works()
        {
            // Arrange
            var sut = new GatewayRoute();

            // Act
            var clone = sut.DeepClone();

            // Assert
            clone.Should().NotBeSameAs(sut);
            clone.RouteId.Should().BeNull();
            clone.Rule.Should().BeNull();
            clone.Priority.Should().BeNull();
            clone.BackendId.Should().BeNull();
            clone.Metadata.Should().BeNull();
        }
    }
}

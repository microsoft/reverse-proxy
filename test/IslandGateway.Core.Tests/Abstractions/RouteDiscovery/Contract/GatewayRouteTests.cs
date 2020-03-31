// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Abstractions.Tests
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
                Match =
                {
                    Methods = new[] { "GET", "POST" },
                    Host = "example.com",
                    Path = "/",
                },
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
            clone.Match.Should().NotBeSameAs(sut.Match);
            clone.Match.Methods.Should().NotBeSameAs(sut.Match.Methods);
            clone.Match.Methods.Should().BeEquivalentTo(sut.Match.Methods);
            clone.Match.Host.Should().Be(sut.Match.Host);
            clone.Match.Path.Should().Be(sut.Match.Path);
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
            clone.Match.Methods.Should().BeNull();
            clone.Match.Host.Should().BeNull();
            clone.Match.Path.Should().BeNull();
            clone.Priority.Should().BeNull();
            clone.BackendId.Should().BeNull();
            clone.Metadata.Should().BeNull();
        }
    }
}

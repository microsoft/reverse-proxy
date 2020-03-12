// <copyright file="BackendEndpointTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace IslandGateway.Core.Abstractions.Tests
{
    public class BackendEndpointTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new BackendEndpoint();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var sut = new BackendEndpoint
            {
                EndpointId = "ep1",
                Address = "https://127.0.0.1:123/a",
                Metadata = new Dictionary<string, string>
                {
                    { "key", "value" },
                },
            };

            // Act
            var clone = sut.DeepClone();

            // Assert
            clone.Should().NotBeSameAs(sut);
            clone.EndpointId.Should().Be(sut.EndpointId);
            clone.Address.Should().Be(sut.Address);
            clone.Metadata.Should().NotBeNull();
            clone.Metadata.Should().NotBeSameAs(sut.Metadata);
            clone.Metadata["key"].Should().Be("value");
        }

        [Fact]
        public void DeepClone_Nulls_Works()
        {
            // Arrange
            var sut = new BackendEndpoint();

            // Act
            var clone = sut.DeepClone();

            // Assert
            clone.Should().NotBeSameAs(sut);
            clone.EndpointId.Should().BeNull();
            clone.Address.Should().BeNull();
            clone.Metadata.Should().BeNull();
        }
    }
}

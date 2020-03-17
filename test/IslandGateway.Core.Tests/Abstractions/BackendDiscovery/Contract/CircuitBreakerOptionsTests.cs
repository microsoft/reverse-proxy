// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Xunit;

namespace IslandGateway.Core.Abstractions.Tests
{
    public class CircuitBreakerOptionsTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new CircuitBreakerOptions();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var sut = new CircuitBreakerOptions
            {
                MaxConcurrentRequests = 10,
                MaxConcurrentRetries = 5,
            };

            // Act
            var clone = sut.DeepClone();

            // Assert
            clone.Should().NotBeSameAs(sut);
            clone.MaxConcurrentRequests.Should().Be(sut.MaxConcurrentRequests);
            clone.MaxConcurrentRetries.Should().Be(sut.MaxConcurrentRetries);
        }
    }
}

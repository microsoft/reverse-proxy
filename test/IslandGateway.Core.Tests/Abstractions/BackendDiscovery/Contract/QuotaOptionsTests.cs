// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Xunit;

namespace IslandGateway.Core.Abstractions.Tests
{
    public class QuotaOptionsTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new QuotaOptions();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var sut = new QuotaOptions
            {
                Average = 10,
                Burst = 100,
            };

            // Act
            var clone = sut.DeepClone();

            // Assert
            clone.Should().NotBeSameAs(sut);
            clone.Average.Should().Be(sut.Average);
            clone.Burst.Should().Be(sut.Burst);
        }
    }
}

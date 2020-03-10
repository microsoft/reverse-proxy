// <copyright file="HealthCheckOptionsTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using FluentAssertions;
using Xunit;

namespace IslandGateway.Core.Abstractions.Tests
{
    public class HealthCheckOptionsTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new HealthCheckOptions();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var sut = new HealthCheckOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Port = 123,
                Path = "/a",
            };

            // Act
            var clone = sut.DeepClone();

            // Assert
            clone.Should().NotBeSameAs(sut);
            clone.Enabled.Should().Be(sut.Enabled);
            clone.Interval.Should().Be(sut.Interval);
            clone.Timeout.Should().Be(sut.Timeout);
            clone.Port.Should().Be(sut.Port);
            clone.Path.Should().Be(sut.Path);
        }
    }
}

// <copyright file="LoadBalancingOptionsTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;

namespace IslandGateway.Core.Abstractions.Tests
{
    public class LoadBalancingOptionsTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new LoadBalancingOptions();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var sut = new LoadBalancingOptions
            {
            };

            // Act
            var clone = sut.DeepClone();

            // Assert
            clone.Should().NotBeSameAs(sut);
        }
    }
}

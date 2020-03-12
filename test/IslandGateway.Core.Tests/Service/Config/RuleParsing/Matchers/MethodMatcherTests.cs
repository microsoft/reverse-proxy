// <copyright file="MethodMatcherTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using FluentAssertions;
using Xunit;

namespace IslandGateway.Core.Service.Tests
{
    public class MethodMatcherTests
    {
        [Fact]
        public void Constructor_Works()
        {
            // arrange
            var methods = new[] { "GET" };

            // Act
            var matcher = new MethodMatcher("Method", methods);

            // Assert
            matcher.Methods.Should().BeSameAs(methods);
        }

        [Fact]
        public void Constructor_InvalidArgCount_Throws()
        {
            // Arrange
            Action action = () => new MethodMatcher("Method", new string[0]);

            // Act & Assert
            action.Should().ThrowExactly<ArgumentException>();
        }
    }
}

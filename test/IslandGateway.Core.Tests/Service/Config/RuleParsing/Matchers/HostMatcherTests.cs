// <copyright file="HostMatcherTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using FluentAssertions;
using Xunit;

namespace IslandGateway.Core.Service.Tests
{
    public class HostMatcherTests
    {
        [Fact]
        public void Constructor_Works()
        {
            // arrange
            const string TestHost = "example.com";

            // Act
            var matcher = new HostMatcher("Host", new[] { TestHost });

            // Assert
            matcher.Host.Should().Be(TestHost);
        }

        [Fact]
        public void Constructor_InvalidArgCount_Throws()
        {
            // Arrange
            Action action1 = () => new HostMatcher("Host", new string[0]);
            Action action2 = () => new HostMatcher("Host", new[] { "a", "b" });

            // Act & Assert
            action1.Should().ThrowExactly<ArgumentException>();
            action2.Should().ThrowExactly<ArgumentException>();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Constructor_EmptyHostName_Throws(string hostName)
        {
            // Arrange
            Action action = () => new HostMatcher("Host", new[] { hostName });

            // Act & Assert
            action.Should().ThrowExactly<ArgumentNullException>();
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            var matcher = new HostMatcher(TestHost);

            // Assert
            matcher.Host.Should().Be(TestHost);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Constructor_EmptyHostName_Throws(string hostName)
        {
            // Arrange
            Action action = () => new HostMatcher(hostName);

            // Act & Assert
            action.Should().ThrowExactly<ArgumentNullException>();
        }
    }
}

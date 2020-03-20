// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using FluentAssertions;
using Xunit;

namespace IslandGateway.Core.Service.Tests
{
    public class PathMatcherTests
    {
        [Fact]
        public void Constructor_Works()
        {
            // arrange
            const string TestPattern = "/a";

            // Act
            var matcher = new PathMatcher(TestPattern);

            // Assert
            matcher.Pattern.Should().Be(TestPattern);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Constructor_EmptyHostName_Throws(string hostName)
        {
            // Arrange
            Action action = () => new PathMatcher(hostName);

            // Act & Assert
            action.Should().ThrowExactly<ArgumentNullException>();
        }
    }
}

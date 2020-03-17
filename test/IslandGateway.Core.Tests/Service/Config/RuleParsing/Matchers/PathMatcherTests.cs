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
            var matcher = new PathMatcher("Path", new[] { TestPattern });

            // Assert
            matcher.Pattern.Should().Be(TestPattern);
        }

        [Fact]
        public void Constructor_InvalidArgCount_Throws()
        {
            // Arrange
            Action action1 = () => new PathMatcher("Path", new string[0]);
            Action action2 = () => new PathMatcher("Path", new[] { "a", "b" });

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
            Action action = () => new PathMatcher("Path", new[] { hostName });

            // Act & Assert
            action.Should().ThrowExactly<ArgumentNullException>();
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
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
            Assert.NotSame(sut, clone);
            Assert.Equal(sut.Enabled, clone.Enabled);
            Assert.Equal(sut.Interval, clone.Interval);
            Assert.Equal(sut.Timeout, clone.Timeout);
            Assert.Equal(sut.Port, clone.Port);
            Assert.Equal(sut.Path, clone.Path);
        }

        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            // Arrange
            var options1 = new HealthCheckOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Port = 123,
                Path = "/a",
            };

            var options2 = new HealthCheckOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Port = 123,
                Path = "/a",
            };

            // Act
            var equals = HealthCheckOptions.Equals(options1, options2);

            // Assert
            Assert.True(equals);
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {
            // Arrange
            var options1 = new HealthCheckOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Port = 123,
                Path = "/a",
            };

            var options2 = new HealthCheckOptions
            {
                Enabled = false,
                Interval = TimeSpan.FromSeconds(4),
                Timeout = TimeSpan.FromSeconds(2),
                Port = 246,
                Path = "/b",
            };

            // Act
            var equals = HealthCheckOptions.Equals(options1, options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_First_Null_Returns_False()
        {
            // Arrange
            var options2 = new HealthCheckOptions
            {
                Enabled = false,
                Interval = TimeSpan.FromSeconds(4),
                Timeout = TimeSpan.FromSeconds(2),
                Port = 246,
                Path = "/b",
            };

            // Act
            var equals = HealthCheckOptions.Equals(null, options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            // Arrange
            var options1 = new HealthCheckOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Port = 123,
                Path = "/a",
            };

            // Act
            var equals = HealthCheckOptions.Equals(options1, null);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_Both_Null_Returns_True()
        {
            // Arrange

            // Act
            var equals = HealthCheckOptions.Equals(null, null);

            // Assert
            Assert.True(equals);
        }
    }
}

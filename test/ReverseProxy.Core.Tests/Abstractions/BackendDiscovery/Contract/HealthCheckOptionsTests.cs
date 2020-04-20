// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Abstractions.Tests
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
    }
}

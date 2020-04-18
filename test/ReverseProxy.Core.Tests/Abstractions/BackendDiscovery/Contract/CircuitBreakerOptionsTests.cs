// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Core.Abstractions.Tests
{
    public class CircuitBreakerOptionsTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new CircuitBreakerOptions();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var sut = new CircuitBreakerOptions
            {
                MaxConcurrentRequests = 10,
                MaxConcurrentRetries = 5,
            };

            // Act
            var clone = sut.DeepClone();

            // Assert
            Assert.NotEqual(sut, clone);
            Assert.Equal(sut.MaxConcurrentRequests, clone.MaxConcurrentRequests);
            Assert.Equal(sut.MaxConcurrentRetries, clone.MaxConcurrentRetries);
        }
    }
}


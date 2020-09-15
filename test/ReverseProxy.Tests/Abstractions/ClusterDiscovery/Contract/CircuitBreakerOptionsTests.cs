// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
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
            Assert.NotSame(sut, clone);
            Assert.Equal(sut.MaxConcurrentRequests, clone.MaxConcurrentRequests);
            Assert.Equal(sut.MaxConcurrentRetries, clone.MaxConcurrentRetries);
        }

        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            // Arrange
            var options1 = new CircuitBreakerOptions
            {
                MaxConcurrentRequests = 10,
                MaxConcurrentRetries = 5,
            };

            var options2 = new CircuitBreakerOptions
            {
                MaxConcurrentRequests = 10,
                MaxConcurrentRetries = 5,
            };

            // Act
            var equals = CircuitBreakerOptions.Equals(options1, options2);

            // Assert
            Assert.True(equals);
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {
            // Arrange
            var options1 = new CircuitBreakerOptions
            {
                MaxConcurrentRequests = 10,
                MaxConcurrentRetries = 5,
            };

            var options2 = new CircuitBreakerOptions
            {
                MaxConcurrentRequests = 20,
                MaxConcurrentRetries = 10,
            };

            // Act
            var equals = CircuitBreakerOptions.Equals(options1, options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_First_Null_Returns_False()
        {
            // Arrange
            var options2 = new CircuitBreakerOptions
            {
                MaxConcurrentRequests = 20,
                MaxConcurrentRetries = 10,
            };

            // Act
            var equals = CircuitBreakerOptions.Equals(null, options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            // Arrange
            var options1 = new CircuitBreakerOptions
            {
                MaxConcurrentRequests = 10,
                MaxConcurrentRetries = 5,
            };

            // Act
            var equals = CircuitBreakerOptions.Equals(options1, null);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_Both_Null_Returns_True()
        {
            // Arrange

            // Act
            var equals = CircuitBreakerOptions.Equals(null, null);

            // Assert
            Assert.True(equals);
        }
    }
}


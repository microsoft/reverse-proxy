// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
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
            Assert.NotSame(sut, clone);
        }

        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            // Arrange
            var options1 = new LoadBalancingOptions
            {
                Mode = LoadBalancingMode.First
            };

            var options2 = new LoadBalancingOptions
            {
                Mode = LoadBalancingMode.First
            };

            // Act
            var equals = LoadBalancingOptions.Equals(options1, options2);

            // Assert
            Assert.True(equals);
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {
            // Arrange
            var options1 = new LoadBalancingOptions
            {
                Mode = LoadBalancingMode.First
            };

            var options2 = new LoadBalancingOptions
            {
                Mode = LoadBalancingMode.PowerOfTwoChoices
            };

            // Act
            var equals = LoadBalancingOptions.Equals(options1, options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_First_Null_Returns_False()
        {
            // Arrange
            var options2 = new LoadBalancingOptions
            {
                Mode = LoadBalancingMode.PowerOfTwoChoices
            };

            // Act
            var equals = LoadBalancingOptions.Equals(null, options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            // Arrange
            var options1 = new LoadBalancingOptions
            {
                Mode = LoadBalancingMode.First
            };

            // Act
            var equals = LoadBalancingOptions.Equals(options1, null);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_Both_Null_Returns_True()
        {
            // Arrange

            // Act
            var equals = LoadBalancingOptions.Equals(null, null);

            // Assert
            Assert.True(equals);
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
{
    public class ClusterPartitioningOptionsTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new ClusterPartitioningOptions();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var sut = new ClusterPartitioningOptions
            {
                PartitionCount = 10,
                PartitionKeyExtractor = "Header('x-ms-org-id')",
                PartitioningAlgorithm = "alg1",
            };

            // Act
            var clone = sut.DeepClone();

            // Assert
            Assert.NotSame(sut, clone);
            Assert.Equal(sut.PartitionCount, clone.PartitionCount);
            Assert.Equal(sut.PartitionKeyExtractor, clone.PartitionKeyExtractor);
            Assert.Equal(sut.PartitioningAlgorithm, clone.PartitioningAlgorithm);
        }

        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            // Arrange
            var options1 = new ClusterPartitioningOptions
            {
                PartitionCount = 10,
                PartitionKeyExtractor = "Header('x-ms-org-id')",
                PartitioningAlgorithm = "alg1",
            };

            var options2 = new ClusterPartitioningOptions
            {
                PartitionCount = 10,
                PartitionKeyExtractor = "Header('x-ms-org-id')",
                PartitioningAlgorithm = "alg1",
            };

            // Act
            var equals = ClusterPartitioningOptions.Equals(options1, options2);

            // Assert
            Assert.True(equals);
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {
            // Arrange
            var options1 = new ClusterPartitioningOptions
            {
                PartitionCount = 10,
                PartitionKeyExtractor = "Header('x-ms-org-id')",
                PartitioningAlgorithm = "alg1",
            };

            var options2 = new ClusterPartitioningOptions
            {
                PartitionCount = 20,
                PartitionKeyExtractor = "Header('x-ms-org-code')",
                PartitioningAlgorithm = "alg2",
            };

            // Act
            var equals = ClusterPartitioningOptions.Equals(options1, options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_First_Null_Returns_False()
        {
            // Arrange
            var options2 = new ClusterPartitioningOptions
            {
                PartitionCount = 20,
                PartitionKeyExtractor = "Header('x-ms-org-code')",
                PartitioningAlgorithm = "alg2",
            };

            // Act
            var equals = ClusterPartitioningOptions.Equals(null, options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            // Arrange
            var options1 = new ClusterPartitioningOptions
            {
                PartitionCount = 10,
                PartitionKeyExtractor = "Header('x-ms-org-id')",
                PartitioningAlgorithm = "alg1",
            };

            // Act
            var equals = ClusterPartitioningOptions.Equals(options1, null);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_Both_Null_Returns_True()
        {
            // Arrange

            // Act
            var equals = ClusterPartitioningOptions.Equals(null, null);

            // Assert
            Assert.True(equals);
        }
    }
}

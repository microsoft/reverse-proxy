// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Core.Abstractions.Tests
{
    public class QuotaOptionsTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new QuotaOptions();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var sut = new QuotaOptions
            {
                Average = 10,
                Burst = 100,
            };

            // Act
            var clone = sut.DeepClone();

            // Assert
            Assert.NotEqual(sut, clone);
            Assert.Equal(sut.Average,clone.Average);
            Assert.Equal(sut.Burst,clone.Burst);
        }
    }
}

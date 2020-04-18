// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Core.Abstractions.Tests
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
            Assert.NotEqual(sut, clone);
        }
    }
}

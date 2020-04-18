// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Abstractions.Tests
{
    public class BackendEndpointTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new BackendEndpoint();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var sut = new BackendEndpoint
            {
                Address = "https://127.0.0.1:123/a",
                Metadata = new Dictionary<string, string>
                {
                    { "key", "value" },
                },
            };

            // Act
            var clone = sut.DeepClone();

            // Assert
            Assert.NotEqual(sut, clone);
            Assert.Equal(sut.Address, clone.Address);
            Assert.NotNull(clone.Metadata);
            Assert.NotStrictEqual(sut.Metadata, clone.Metadata);
            Assert.Equal("value", clone.Metadata["key"]);
        }

        [Fact]
        public void DeepClone_Nulls_Works()
        {
            // Arrange
            var sut = new BackendEndpoint();

            // Act
            var clone = sut.DeepClone();

            // Assert
            Assert.NotEqual(sut, clone);
            Assert.Null(clone.Address);
            Assert.Null(clone.Metadata);
        }
    }
}

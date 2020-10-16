// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Service.Management.Tests
{
    /// <summary>
    /// Tests for the <see cref="DestinationManager"/> class.
    /// Additional scenarios are covered in <see cref="ItemManagerBaseTests"/>.
    /// </summary>
    public class DestinationManagerTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new DestinationManager();
        }

        [Fact]
        public void GetOrCreateItem_NonExistentItem_CreatesNewItem()
        {
            // Arrange
            var manager = new DestinationManager();

            // Act
            var item = manager.GetOrCreateItem("abc", item => { });

            // Assert
            Assert.NotNull(item);
            Assert.Equal("abc", item.DestinationId);
        }
    }
}

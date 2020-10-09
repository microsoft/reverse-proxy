// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;
using Moq;
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
            new DestinationManager(null);
        }

        [Fact]
        public void GetOrCreateItem_NonExistentItem_CreatesNewItem()
        {
            var changeListeners = new[] { new Mock<IDestinationChangeListener>().Object, new Mock<IDestinationChangeListener>().Object };

            // Arrange
            var manager = new DestinationManager(changeListeners);

            // Act
            var item = manager.GetOrCreateItem("abc", item => { });

            // Assert
            Assert.NotNull(item);
            Assert.Equal("abc", item.DestinationId);

            Assert.True(false, "Test destination change listeners.");
        }
    }
}

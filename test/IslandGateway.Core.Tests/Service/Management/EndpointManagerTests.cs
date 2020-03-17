// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Xunit;

namespace IslandGateway.Core.Service.Management.Tests
{
    /// <summary>
    /// Tests for the <see cref="EndpointManager"/> class.
    /// Additional scenarios are covered in <see cref="ItemManagerBaseTests"/>.
    /// </summary>
    public class EndpointManagerTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new EndpointManager();
        }

        [Fact]
        public void GetOrCreateItem_NonExistentItem_CreatesNewItem()
        {
            // Arrange
            var manager = new EndpointManager();

            // Act
            var item = manager.GetOrCreateItem("abc", item => { });

            // Assert
            item.Should().NotBeNull();
            item.EndpointId.Should().Be("abc");
        }
    }
}

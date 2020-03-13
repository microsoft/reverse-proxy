// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using IslandGateway.Signals;
using Xunit;

namespace IslandGateway.Core.Service.Management.Tests
{
    public class ItemManagerBaseTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new TestItemManager();
        }

        [Fact]
        public void TryGetItem_NonExistentItem_ReturnsNull()
        {
            // Arrange
            var manager = new TestItemManager();

            // Act
            var item = manager.TryGetItem("abc");

            // Assert
            item.Should().BeNull();
        }

        [Fact]
        public void TryGetItem_ExistingItem_Works()
        {
            // Arrange
            var manager = new TestItemManager();
            manager.GetOrCreateItem("abc", item => item.Value = 1);

            // Act
            var item = manager.TryGetItem("abc");

            // Assert
            item.Should().NotBeNull();
            item.ItemId.Should().Be("abc");
            item.Value.Should().Be(1);
        }

        [Fact]
        public void TryGetItem_CaseSensitive_ReturnsCorrectItem()
        {
            // Arrange
            var manager = new TestItemManager();
            var item1 = manager.GetOrCreateItem("abc", item => item.Value = 1);
            var item2 = manager.GetOrCreateItem("ABC", item => item.Value = 2);

            // Act
            var actual1 = manager.TryGetItem("abc");
            var actual2 = manager.TryGetItem("ABC");
            var actual3 = manager.TryGetItem("aBc");

            // Assert
            item1.Should().NotBeNull();
            item2.Should().NotBeNull();
            item1.Should().NotBeSameAs(item2);

            actual1.Should().NotBeNull();
            actual1.Should().BeSameAs(item1);

            actual2.Should().NotBeNull();
            actual2.Should().BeSameAs(item2);

            actual3.Should().BeNull();
        }

        [Fact]
        public void GetOrCreateItem_CreatesAndInitializes_NonExistentItem()
        {
            // Arrange
            var manager = new TestItemManager();

            // Act
            var item1 = manager.GetOrCreateItem("abc", item => item.Value = 1);
            var item2 = manager.GetOrCreateItem("def", item => item.Value = 2);

            // Assert
            item1.Should().NotBeNull();
            item1.ItemId.Should().Be("abc");
            item1.Value.Should().Be(1);

            item2.Should().NotBeNull();
            item2.ItemId.Should().Be("def");
            item2.Value.Should().Be(2);
        }

        [Fact]
        public void GetOrCreateItem_ReusesAndReinitializes_ExistingItem()
        {
            // Arrange
            var manager = new TestItemManager();

            // Act
            var item1 = manager.GetOrCreateItem(
                "abc",
                item =>
                {
                    item.Value.Should().Be(0);
                    item.Value = 1;
                });
            var item2 = manager.GetOrCreateItem(
                "abc",
                item =>
                {
                    item.Value.Should().Be(1);
                    item.Value = 2;
                });

            // Assert
            item1.Should().NotBeNull();
            item1.ItemId.Should().Be("abc");
            item1.Value.Should().Be(2);
            item1.Should().BeSameAs(item2);
        }

        [Fact]
        public void GetOrCreateItem_CreatesNew_PreviouslyRemovedItem()
        {
            // Arrange
            var manager = new TestItemManager();

            // Act
            var item1 = manager.GetOrCreateItem(
                "abc",
                item =>
                {
                    item.Value.Should().Be(0);
                    item.Value = 1;
                });
            manager.TryRemoveItem("abc").Should().BeTrue();
            var item2 = manager.GetOrCreateItem(
                "abc",
                item =>
                {
                    item.Value.Should().Be(0);
                    item.Value = 2;
                });

            // Assert
            item1.Should().NotBeNull();
            item1.ItemId.Should().Be("abc");
            item1.Value.Should().Be(1);

            item2.Should().NotBeNull();
            item2.ItemId.Should().Be("abc");
            item2.Value.Should().Be(2);
        }

        [Fact]
        public void GetOrCreateItem_DoesNotAdd_WhenSetupActionThrows()
        {
            // Arrange
            var manager = new TestItemManager();

            // Act
            Action action = () => manager.GetOrCreateItem("abc", item => throw new Exception());

            // Assert
            action.Should().ThrowExactly<Exception>();
            manager.GetItems().Should().BeEmpty();
        }

        [Fact]
        public void GetOrCreateItem_ThreadSafety()
        {
            // Arrange
            const int Iterations = 100_000;
            var manager = new TestItemManager();

            // Act
            Parallel.For(0, Iterations, i =>
            {
                manager.GetOrCreateItem("abc", item => item.Value++);
            });

            // Assert
            var item = manager.TryGetItem("abc");
            item.Should().NotBeNull();
            item.ItemId.Should().Be("abc");
            item.Value.Should().Be(Iterations);
        }

        [Fact]
        public void GetItems_Works_Empty()
        {
            // Arrange
            var manager = new TestItemManager();

            // Act
            var items = manager.GetItems();

            // Assert
            items.Should().BeEmpty();
        }

        [Fact]
        public void GetItems_Works_OneItem()
        {
            // Arrange
            var manager = new TestItemManager();

            // Act
            var item = manager.GetOrCreateItem("abc", item => { });
            var items = manager.GetItems();

            // Assert
            items.Should().HaveCount(1);
            items[0].Should().BeSameAs(item);
        }

        [Fact]
        public void GetItems_Works_TwoItems()
        {
            // Arrange
            var manager = new TestItemManager();

            // Act
            var item1 = manager.GetOrCreateItem("abc", item => { });
            var item2 = manager.GetOrCreateItem("def", item => { });
            var items = manager.GetItems();

            // Assert
            items.Should().HaveCount(2);
            items.Should().Contain(item1);
            items.Should().Contain(item2);
        }

        [Fact]
        public void TryRemoveItem_Works()
        {
            // Arrange
            var manager = new TestItemManager();

            // Act
            var item = manager.GetOrCreateItem("abc", item => { });
            var result1 = manager.TryRemoveItem("abc");
            var result2 = manager.TryRemoveItem("abc");

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeFalse();
        }

        [Fact]
        public void Items_Notifications_Work()
        {
            // Arrange
            var manager = new TestItemManager();
            var itemsCountSignal = manager.Items.Select(items => items.Count);

            // Act
            manager.GetOrCreateItem("abc", item => { });
            manager.GetOrCreateItem("def", item => { });

            itemsCountSignal.Value.Should().Be(2);

            manager.TryRemoveItem("abc");
            manager.TryRemoveItem("def");

            itemsCountSignal.Value.Should().Be(0);
        }

        private class TestItemManager : ItemManagerBase<Item>
        {
            protected override Item InstantiateItem(string itemId)
            {
                return new Item(itemId);
            }
        }

        private class Item
        {
            public Item(string itemId)
            {
                ItemId = itemId;
            }

            public string ItemId { get; }

            public int Value { get; set; }
        }
    }
}

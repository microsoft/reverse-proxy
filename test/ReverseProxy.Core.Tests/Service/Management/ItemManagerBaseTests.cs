// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Signals;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Management.Tests
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
            Assert.Null(item);
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
            Assert.NotNull(item);
            Assert.Equal("abc", item.ItemId);
            Assert.Equal(1, item.Value);
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
            Assert.NotNull(item1);
            Assert.NotNull(item2);
            Assert.NotSame(item2, item1);

            Assert.NotNull(actual1);
            Assert.Same(item1, actual1);

            Assert.NotNull(actual2);
            Assert.Same(item2, actual2);

            Assert.Null(actual3);
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
            Assert.NotNull(item1);
            Assert.Equal("abc", item1.ItemId);
            Assert.Equal(1, item1.Value);

            Assert.NotNull(item2);
            Assert.Equal("def", item2.ItemId);
            Assert.Equal(2, item2.Value);
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
                    Assert.Equal(0, item.Value);
                    item.Value = 1;
                });
            var item2 = manager.GetOrCreateItem(
                "abc",
                item =>
                {
                    Assert.Equal(1, item.Value);
                    item.Value = 2;
                });

            // Assert
            Assert.NotNull(item1);
            Assert.Equal("abc", item1.ItemId);
            Assert.Equal(2, item1.Value);
            Assert.Same(item2, item1);
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
                    Assert.Equal(0, item.Value);
                    item.Value = 1;
                });
            Assert.True(manager.TryRemoveItem("abc"));
            var item2 = manager.GetOrCreateItem(
                "abc",
                item =>
                {
                    Assert.Equal(0, item.Value);
                    item.Value = 2;
                });

            // Assert
            Assert.NotNull(item1);
            Assert.Equal("abc", item1.ItemId);
            Assert.Equal(1, item1.Value);

            Assert.NotNull(item2);
            Assert.Equal("abc", item2.ItemId);
            Assert.Equal(2, item2.Value);
        }

        [Fact]
        public void GetOrCreateItem_DoesNotAdd_WhenSetupActionThrows()
        {
            // Arrange
            var manager = new TestItemManager();

            // Act
            Action action = () => manager.GetOrCreateItem("abc", item => throw new Exception());

            // Assert
            Assert.Throws<Exception>(action);
            Assert.Empty(manager.GetItems());
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
            Assert.NotNull(item);
            Assert.Equal("abc", item.ItemId);
            Assert.Equal(Iterations, item.Value);
        }

        [Fact]
        public void GetItems_Works_Empty()
        {
            // Arrange
            var manager = new TestItemManager();

            // Act
            var items = manager.GetItems();

            // Assert
            Assert.Empty(items);
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
            Assert.Single(items);
            Assert.Same(item, items[0]);
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
            Assert.Equal(2, items.Count);
            Assert.Contains(item1, items);
            Assert.Contains(item2, items);
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
            Assert.True(result1);
            Assert.False(result2);
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

            Assert.Equal(2, itemsCountSignal.Value);

            manager.TryRemoveItem("abc");
            manager.TryRemoveItem("def");

            Assert.Equal(0, itemsCountSignal.Value);
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

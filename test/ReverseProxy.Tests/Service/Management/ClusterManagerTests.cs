// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.RuntimeModel;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Management.Tests
{
    /// <summary>
    /// Tests for the <see cref="ClusterManager"/> class.
    /// Additional scenarios are covered in <see cref="ItemManagerBaseTests"/>.
    /// </summary>
    public class ClusterManagerTests : TestAutoMockBase
    {
        [Fact]
        public void Constructor_Works()
        {
            Create<ClusterManager>();
        }

        [Fact]
        public void GetOrCreateItem_NonExistentItem_CreatesNewItem()
        {
            // Arrange
            var endpointManager = new DestinationManager();
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(endpointManager);
            var changeListener = new Mock<IClusterChangeListener>();
            Provide(changeListener.Object);
            var manager = Create<ClusterManager>();

            // Act
            var item = manager.GetOrCreateItem("abc", item => { });

            // Assert
            Assert.NotNull(item);
            Assert.Equal("abc", item.ClusterId);
            Assert.Same(endpointManager, item.DestinationManager);
            changeListener.Verify(l => l.OnClusterAdded(item), Times.Once);
            changeListener.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetOrCreateItem_ExistingItem_ChangesItem()
        {
            // Arrange
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(new DestinationManager());
            var changeListener = new Mock<IClusterChangeListener>();
            Provide(changeListener.Object);
            var manager = Create<ClusterManager>();

            // Act
            var item0 = manager.GetOrCreateItem("abc", item => { });
            var item1 = manager.GetOrCreateItem("ddd", item => { });
            var item2 = manager.GetOrCreateItem("abc", item => { });

            // Assert
            Assert.Same(item0, item2);
            Assert.Equal("abc", item0.ClusterId);
            Assert.Equal("ddd", item1.ClusterId);
            changeListener.Verify(l => l.OnClusterAdded(item0), Times.Once);
            changeListener.Verify(l => l.OnClusterAdded(item1), Times.Once);
            changeListener.Verify(l => l.OnClusterChanged(item0), Times.Once);
            changeListener.VerifyNoOtherCalls();
        }

        [Fact]
        public void RemoveItem_ExistingItem_RemovesItem()
        {
            // Arrange
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(new DestinationManager());
            var changeListener = new Mock<IClusterChangeListener>();
            Provide(changeListener.Object);
            var manager = Create<ClusterManager>();

            // Act
            var item0 = manager.GetOrCreateItem("abc", item => { });
            var item1 = manager.GetOrCreateItem("ddd", item => { });
            var removed = manager.TryRemoveItem("abc");

            // Assert
            Assert.True(removed);
            Assert.Equal("abc", item0.ClusterId);
            Assert.Equal("ddd", item1.ClusterId);
            changeListener.Verify(l => l.OnClusterAdded(item0), Times.Once);
            changeListener.Verify(l => l.OnClusterAdded(item1), Times.Once);
            changeListener.Verify(l => l.OnClusterRemoved(item0), Times.Once);
            changeListener.VerifyNoOtherCalls();
        }

        [Fact]
        public void RemoveItem_NonExistentItem_DoNothing()
        {
            // Arrange
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(new DestinationManager());
            var changeListener = new Mock<IClusterChangeListener>();
            Provide(changeListener.Object);
            var manager = Create<ClusterManager>();

            // Act
            var item0 = manager.GetOrCreateItem("abc", item => { });
            var removed = manager.TryRemoveItem("ddd");

            // Assert
            Assert.False(removed);
            Assert.Equal("abc", item0.ClusterId);
            changeListener.Verify(l => l.OnClusterAdded(item0), Times.Once);
            changeListener.VerifyNoOtherCalls();
        }
    }
}

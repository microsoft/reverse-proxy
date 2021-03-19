// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Yarp.ReverseProxy.Service.Management.Tests
{
    /// <summary>
    /// Tests for the <see cref="RouteManager"/> class.
    /// Additional scenarios are covered in <see cref="ItemManagerBaseTests"/>.
    /// </summary>
    public class RouteManagerTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new RouteManager();
        }

        [Fact]
        public void GetOrCreateItem_NonExistentItem_CreatesNewItem()
        {
            var manager = new RouteManager();

            var item = manager.GetOrCreateItem("abc", item => { });

            Assert.NotNull(item);
            Assert.Equal("abc", item.RouteId);
        }
    }
}

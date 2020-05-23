// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.RuntimeModel;
using Xunit;


namespace Microsoft.ReverseProxy.Service.RuntimeModel
{
    public class DestinationInfoTests
    {
        [Fact]
        public void DestinationInfoEnumerator()
        {
            // Arrange
            var destinationInfo = new DestinationInfo("destionation1");
            var list = new List<DestinationInfo>();

            // Act
            foreach (var item in destinationInfo)
            {
                list.Add(item);
            }

            // Assert
            var first = Assert.Single(list);
            Assert.Same(destinationInfo, first);
        }

        [Fact]
        public void DestionationInfoReadOnlyList()
        {
            // Arrange
            var destinationInfo = new DestinationInfo("destionation1");

            // Act
            IReadOnlyList<DestinationInfo> list = destinationInfo;

            // Assert
            Assert.Equal(1, list.Count);
            Assert.Same(destinationInfo, list[0]);
            Assert.Throws<IndexOutOfRangeException>(() => list[1]);
        }
    }
}

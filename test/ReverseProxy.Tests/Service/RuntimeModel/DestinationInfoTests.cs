// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Xunit;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.RuntimeModel
{
    public class DestinationInfoTests
    {
        [Fact]
        public void DestinationInfoEnumerator()
        {
            var destinationInfo = new DestinationInfo("destionation1");
            var list = new List<DestinationInfo>();

            foreach (var item in destinationInfo)
            {
                list.Add(item);
            }

            var first = Assert.Single(list);
            Assert.Same(destinationInfo, first);
        }

        [Fact]
        public void DestionationInfoReadOnlyList()
        {
            var destinationInfo = new DestinationInfo("destionation2");

            IReadOnlyList<DestinationInfo> list = destinationInfo;

            Assert.Equal(1, list.Count);
            Assert.Same(destinationInfo, list[0]);
            Assert.Throws<IndexOutOfRangeException>(() => list[1]);
        }
    }
}

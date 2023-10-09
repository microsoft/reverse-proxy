// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Xunit;

namespace Yarp.ReverseProxy.Model.Tests;

public class DestinationStateTests
{
    [Fact]
    public void DestinationInfoEnumerator()
    {
        var destinationInfo = new DestinationState("destionation1");
        var list = new List<DestinationState>();

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
        var destinationInfo = new DestinationState("destionation2");

        IReadOnlyList<DestinationState> list = destinationInfo;

        Assert.Single(list);
        Assert.Same(destinationInfo, list[0]);
        Assert.Throws<IndexOutOfRangeException>(() => list[1]);
    }
}

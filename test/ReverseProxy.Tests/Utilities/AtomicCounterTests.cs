// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Yarp.ReverseProxy.Utilities.Tests;

public class AtomicCounterTests
{
    [Fact]
    public void Constructor_Works()
    {
        new AtomicCounter();
    }

    [Fact]
    public void Increment_ThreadSafety()
    {
        const int Iterations = 100_000;

        var counter = new AtomicCounter();

        Parallel.For(0, Iterations, i =>
        {
            counter.Increment();
        });

        Assert.Equal(Iterations, counter.Value);
    }

    [Fact]
    public void Decrement_ThreadSafety()
    {
        const int Iterations = 100_000;

        var counter = new AtomicCounter();

        Parallel.For(0, Iterations, i =>
        {
            counter.Decrement();
        });

        Assert.Equal(-Iterations, counter.Value);
    }
}

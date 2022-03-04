// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Yarp.ReverseProxy.Utilities.Tests;

public class ActivityCancellationTokenSourceTests
{
#if NET6_0_OR_GREATER // CancellationTokenSource.TryReset() is only available in 6.0+
    [Fact]
    public void ActivityCancellationTokenSource_PoolsSources()
    {
        // This test can run in parallel with others making use of ActivityCancellationTokenSource
        // A different thread could have already added/removed a source from the queue

        for (var i = 0; i < 1000; i++)
        {
            var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);
            cts.Return();

            var cts2 = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);
            cts2.Return();

            if (ReferenceEquals(cts, cts2))
            {
                return;
            }
        }

        Assert.True(false, "CancellationTokenSources were not pooled");
    }
#endif

    [Fact]
    public void ActivityCancellationTokenSource_DoesNotPoolsCanceledSources()
    {
        var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);
        cts.Cancel();

        var cts2 = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.NotSame(cts, cts2);
    }

    [Fact]
    public void ActivityCancellationTokenSource_RespectsLinkedToken()
    {
        var linkedCts = new CancellationTokenSource();

        var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), linkedCts.Token);
        linkedCts.Cancel();

        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void ActivityCancellationTokenSource_ClearsRegistrations()
    {
        var linkedCts = new CancellationTokenSource();

        var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), linkedCts.Token);
        cts.Return();

        linkedCts.Cancel();

        Assert.False(cts.IsCancellationRequested);
    }

    [Fact]
    public async Task ActivityCancellationTokenSource_RespectsTimeout()
    {
        var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromMilliseconds(1), CancellationToken.None);

        for (var i = 0; i < 1000; i++)
        {
            if (cts.IsCancellationRequested)
            {
                return;
            }

            await Task.Delay(1);
        }

        Assert.True(false, "Cts was not canceled");
    }
}

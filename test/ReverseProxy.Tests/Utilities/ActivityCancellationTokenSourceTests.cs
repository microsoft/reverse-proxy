// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Yarp.ReverseProxy.Utilities.Tests
{
    public class ActivityCancellationTokenSourceTests
    {
        [Fact]
        public void ActivityCancellationTokenSource_PoolsSources()
        {
            var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);

            cts.Return();

            var cts2 = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);

            Assert.Same(cts, cts2);
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

            throw new TimeoutException("Cts was not canceled");
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Fakes;
using Polly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Kubernetes.Controller.Rate;

public class LimiterTests
{
    [Fact]
    public void FirstTokenIsAvailable()
    {
        var clock = new FakeSystemClock();
        var limiter = new Limiter(new Limit(10), 1, clock);

        var allowed = limiter.Allow();

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(1)]
    [InlineData(300)]
    public void AsManyAsBurstTokensAreAvailableRightAway(int burst)
    {
        var clock = new FakeSystemClock();
        var limiter = new Limiter(new Limit(10), burst, clock);

        var allowed = new List<bool>();
        foreach (var index in Enumerable.Range(1, burst))
        {
            allowed.Add(limiter.Allow());
        }
        var notAllowed = limiter.Allow();

        Assert.All(allowed, item => Assert.True(item));
        Assert.False(notAllowed);
    }

    [Fact]
    public void TokensBecomeAvailableAtLimitPerSecondRate()
    {
        var clock = new FakeSystemClock();
        var limiter = new Limiter(new Limit(10), 50, clock);

        var initiallyAllowed = limiter.AllowN(clock.UtcNow, 50);
        var thenNotAllowed1 = limiter.Allow();

        clock.Advance(TimeSpan.FromMilliseconds(100));
        var oneTokenAvailable = limiter.Allow();
        var thenNotAllowed2 = limiter.Allow();

        clock.Advance(TimeSpan.FromMilliseconds(200));
        var twoTokensAvailable1 = limiter.Allow();
        var twoTokensAvailable2 = limiter.Allow();
        var thenNotAllowed3 = limiter.Allow();

        Assert.True(initiallyAllowed);
        Assert.False(thenNotAllowed1);
        Assert.True(oneTokenAvailable);
        Assert.False(thenNotAllowed2);
        Assert.True(twoTokensAvailable1);
        Assert.True(twoTokensAvailable2);
        Assert.False(thenNotAllowed3);
    }

    [Fact]
    public void ReserveTellsYouHowLongToWait()
    {
        var clock = new FakeSystemClock();
        var limiter = new Limiter(new Limit(10), 50, clock);

        var initiallyAllowed = limiter.AllowN(clock.UtcNow, 50);
        var thenNotAllowed1 = limiter.Allow();

        var reserveOne = limiter.Reserve();
        var delayOne = reserveOne.Delay();

        var reserveTwoMore = limiter.Reserve(clock.UtcNow, 2);
        var delayTwoMore = reserveTwoMore.Delay();

        clock.Advance(TimeSpan.FromMilliseconds(450));

        var reserveAlreadyAvailable = limiter.Reserve();
        var delayAlreadyAvailable = reserveAlreadyAvailable.Delay();

        var reserveHalfAvailable = limiter.Reserve();
        var delayHalfAvailable = reserveHalfAvailable.Delay();

        Assert.True(initiallyAllowed);
        Assert.False(thenNotAllowed1);
        Assert.True(reserveOne.Ok);
        Assert.Equal(TimeSpan.FromMilliseconds(100), delayOne);
        Assert.True(reserveTwoMore.Ok);
        Assert.Equal(TimeSpan.FromMilliseconds(300), delayTwoMore);
        Assert.True(reserveAlreadyAvailable.Ok);
        Assert.Equal(TimeSpan.Zero, delayAlreadyAvailable);
        Assert.True(reserveHalfAvailable.Ok);
        Assert.Equal(TimeSpan.FromMilliseconds(50), delayHalfAvailable);
    }

    [Fact(Skip = "https://github.com/microsoft/reverse-proxy/issues/1357")]
    public async Task WaitAsyncCausesPauseLikeReserve()
    {
        var limiter = new Limiter(new Limit(10), 5);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (cancellation.IsCancellationRequested == false)
        {
            var task = limiter.WaitAsync(cancellation.Token);
            if (!task.IsCompleted)
            {
                await task.ConfigureAwait(false);
                break;
            }
        }

        var delayOne = new Stopwatch();
        delayOne.Start();
        await limiter.WaitAsync(cancellation.Token).ConfigureAwait(false);
        delayOne.Stop();

        var delayTwoMore = new Stopwatch();
        delayTwoMore.Start();
        await limiter.WaitAsync(2, cancellation.Token).ConfigureAwait(false);
        delayTwoMore.Stop();

        await Task.Delay(TimeSpan.FromMilliseconds(150)).ConfigureAwait(false);

        var delayAlreadyAvailable = new Stopwatch();
        delayAlreadyAvailable.Start();
        await limiter.WaitAsync(cancellation.Token).ConfigureAwait(false);
        delayAlreadyAvailable.Stop();

        var delayHalfAvailable = new Stopwatch();
        delayHalfAvailable.Start();
        await limiter.WaitAsync(cancellation.Token).ConfigureAwait(false);
        delayHalfAvailable.Stop();

        Assert.InRange(delayOne.Elapsed, TimeSpan.FromMilliseconds(75), TimeSpan.FromMilliseconds(125));
        Assert.InRange(delayTwoMore.Elapsed, TimeSpan.FromMilliseconds(175), TimeSpan.FromMilliseconds(225));
        Assert.InRange(delayAlreadyAvailable.Elapsed, TimeSpan.Zero, TimeSpan.FromMilliseconds(5));
        Assert.InRange(delayHalfAvailable.Elapsed, TimeSpan.FromMilliseconds(25), TimeSpan.FromMilliseconds(75));
    }

    [Fact(Skip = "https://github.com/microsoft/reverse-proxy/issues/1357")]
    public async Task ManyWaitsStackUp()
    {
        await Policy
            .Handle<InRangeException>()
            .RetryAsync(3)
            .ExecuteAsync(async () =>
            {
                var limiter = new Limiter(new Limit(10), 5);
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                while (cancellation.IsCancellationRequested == false)
                {
                    var task = limiter.WaitAsync(cancellation.Token);
                    if (!task.IsCompleted)
                    {
                        await task.ConfigureAwait(false);
                        break;
                    }
                }

                var delayOne = new Stopwatch();
                delayOne.Start();

                var delayTwo = new Stopwatch();
                delayTwo.Start();

                var delayThree = new Stopwatch();
                delayThree.Start();

                var waits = new List<Task>
                {
                    limiter.WaitAsync(cancellation.Token),
                    limiter.WaitAsync(cancellation.Token),
                    limiter.WaitAsync(cancellation.Token),
                };

                var taskOne = await Task.WhenAny(waits).ConfigureAwait(false);
                await taskOne.ConfigureAwait(false);
                delayOne.Stop();
                waits.Remove(taskOne);

                var taskTwo = await Task.WhenAny(waits).ConfigureAwait(false);
                await taskTwo.ConfigureAwait(false);
                delayTwo.Stop();
                waits.Remove(taskTwo);

                var taskThree = await Task.WhenAny(waits).ConfigureAwait(false);
                await taskThree.ConfigureAwait(false);
                delayThree.Stop();
                waits.Remove(taskThree);

                Assert.InRange(delayOne.Elapsed, TimeSpan.FromMilliseconds(75), TimeSpan.FromMilliseconds(125));
                Assert.InRange(delayTwo.Elapsed, TimeSpan.FromMilliseconds(175), TimeSpan.FromMilliseconds(225));
                Assert.InRange(delayThree.Elapsed, TimeSpan.FromMilliseconds(275), TimeSpan.FromMilliseconds(325));
            }).ConfigureAwait(false);
    }
}

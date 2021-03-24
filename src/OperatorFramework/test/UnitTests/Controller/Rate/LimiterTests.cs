// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Polly;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Controller.Rate
{
    [TestClass]
    public class LimiterTests
    {
        [TestMethod]
        public void FirstTokenIsAvailable()
        {
            // arrange
            var clock = new FakeSystemClock();
            var limiter = new Limiter(new Limit(10), 1, clock);

            // act
            var allowed = limiter.Allow();

            // assert
            allowed.ShouldBe(true);
        }

        [TestMethod]
        [DataRow(5)]
        [DataRow(1)]
        [DataRow(300)]
        public void AsManyAsBurstTokensAreAvailableRightAway(int burst)
        {
            // arrange
            var clock = new FakeSystemClock();
            var limiter = new Limiter(new Limit(10), burst, clock);

            // act
            var allowed = new List<bool>();
            foreach (var index in Enumerable.Range(1, burst))
            {
                allowed.Add(limiter.Allow());
            }
            var notAllowed = limiter.Allow();

            // assert
            allowed.ShouldAllBe(item => item == true);
            notAllowed.ShouldBeFalse();
        }

        [TestMethod]
        public void TokensBecomeAvailableAtLimitPerSecondRate()
        {
            // arrange
            var clock = new FakeSystemClock();
            var limiter = new Limiter(new Limit(10), 50, clock);

            // act
            var initiallyAllowed = limiter.AllowN(clock.UtcNow, 50);
            var thenNotAllowed1 = limiter.Allow();

            clock.Advance(TimeSpan.FromMilliseconds(100));
            var oneTokenAvailable = limiter.Allow();
            var thenNotAllowed2 = limiter.Allow();

            clock.Advance(TimeSpan.FromMilliseconds(200));
            var twoTokensAvailable1 = limiter.Allow();
            var twoTokensAvailable2 = limiter.Allow();
            var thenNotAllowed3 = limiter.Allow();

            // assert
            initiallyAllowed.ShouldBeTrue();
            thenNotAllowed1.ShouldBeFalse();
            oneTokenAvailable.ShouldBeTrue();
            thenNotAllowed2.ShouldBeFalse();
            twoTokensAvailable1.ShouldBeTrue();
            twoTokensAvailable2.ShouldBeTrue();
            thenNotAllowed3.ShouldBeFalse();
        }

        [TestMethod]
        public void ReserveTellsYouHowLongToWait()
        {
            // arrange
            var clock = new FakeSystemClock();
            var limiter = new Limiter(new Limit(10), 50, clock);

            // act
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

            // assert
            initiallyAllowed.ShouldBeTrue();
            thenNotAllowed1.ShouldBeFalse();
            reserveOne.Ok.ShouldBeTrue();
            delayOne.ShouldBe(TimeSpan.FromMilliseconds(100));
            reserveTwoMore.Ok.ShouldBeTrue();
            delayTwoMore.ShouldBe(TimeSpan.FromMilliseconds(300));
            reserveAlreadyAvailable.Ok.ShouldBeTrue();
            delayAlreadyAvailable.ShouldBe(TimeSpan.Zero);
            reserveHalfAvailable.Ok.ShouldBeTrue();
            delayHalfAvailable.ShouldBe(TimeSpan.FromMilliseconds(50));
        }

        [TestMethod]
        public async Task WaitAsyncCausesPauseLikeReserve()
        {
            // arrange
            var limiter = new Limiter(new Limit(10), 5);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // act
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

            // assert
            delayOne.Elapsed.ShouldBe(TimeSpan.FromMilliseconds(100), tolerance: TimeSpan.FromMilliseconds(25));
            delayTwoMore.Elapsed.ShouldBe(TimeSpan.FromMilliseconds(200), tolerance: TimeSpan.FromMilliseconds(25));
            delayAlreadyAvailable.Elapsed.ShouldBe(TimeSpan.Zero, tolerance: TimeSpan.FromMilliseconds(5));
            delayHalfAvailable.Elapsed.ShouldBe(TimeSpan.FromMilliseconds(50), tolerance: TimeSpan.FromMilliseconds(25));
        }

        [TestMethod]
        public async Task ManyWaitsStackUp()
        {
            await Policy
                .Handle<ShouldAssertException>()
                .RetryAsync(3)
                .ExecuteAsync(async () =>
                {
                    // arrange
                    var limiter = new Limiter(new Limit(10), 5);
                    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                    // act
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

                    var taskOne = await Task.WhenAny(waits.ToArray()).ConfigureAwait(false);
                    await taskOne.ConfigureAwait(false);
                    delayOne.Stop();
                    waits.Remove(taskOne);

                    var taskTwo = await Task.WhenAny(waits.ToArray()).ConfigureAwait(false);
                    await taskTwo.ConfigureAwait(false);
                    delayTwo.Stop();
                    waits.Remove(taskTwo);

                    var taskThree = await Task.WhenAny(waits.ToArray()).ConfigureAwait(false);
                    await taskThree.ConfigureAwait(false);
                    delayThree.Stop();
                    waits.Remove(taskThree);

                    // assert
                    delayOne.Elapsed.ShouldBe(TimeSpan.FromMilliseconds(100), tolerance: TimeSpan.FromMilliseconds(25));
                    delayTwo.Elapsed.ShouldBe(TimeSpan.FromMilliseconds(200), tolerance: TimeSpan.FromMilliseconds(25));
                    delayThree.Elapsed.ShouldBe(TimeSpan.FromMilliseconds(300), tolerance: TimeSpan.FromMilliseconds(25));
                }).ConfigureAwait(false);
        }
    }
}

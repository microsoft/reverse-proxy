// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Controller.Rate;
using Microsoft.Kubernetes.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;

namespace Microsoft.Kubernetes.Controllers.Rate
{
    [TestClass]
    public class ReservationTests
    {
        [TestMethod]
        public void NotOkayAlwaysReturnsMaxValueDelay()
        {
            // arrange 
            var clock = new FakeSystemClock();
            var reservation = new Reservation(
                clock: clock,
                limiter: default,
                ok: false);

            // act
            var delay1 = reservation.Delay();
            var delayFrom1 = reservation.DelayFrom(clock.UtcNow);
            clock.Advance(TimeSpan.FromMinutes(3));
            var delay2 = reservation.Delay();
            var delayFrom2 = reservation.DelayFrom(clock.UtcNow);

            // assert
            delay1.ShouldBe(TimeSpan.MaxValue);
            delayFrom1.ShouldBe(TimeSpan.MaxValue);
            delay2.ShouldBe(TimeSpan.MaxValue);
            delayFrom2.ShouldBe(TimeSpan.MaxValue);
        }

        [TestMethod]
        public void DelayIsZeroWhenTimeToActIsNowOrEarlier()
        {
            // arrange 
            var clock = new FakeSystemClock();
            var reservation = new Reservation(
                clock: clock,
                limiter: default,
                ok: true,
                timeToAct: clock.UtcNow,
                limit: default);

            // act
            var delay1 = reservation.Delay();
            var delayFrom1 = reservation.DelayFrom(clock.UtcNow);
            clock.Advance(TimeSpan.FromMinutes(3));
            var delay2 = reservation.Delay();
            var delayFrom2 = reservation.DelayFrom(clock.UtcNow);

            // assert
            delay1.ShouldBe(TimeSpan.Zero);
            delayFrom1.ShouldBe(TimeSpan.Zero);
            delay2.ShouldBe(TimeSpan.Zero);
            delayFrom2.ShouldBe(TimeSpan.Zero);
        }

        [TestMethod]
        public void DelayGetsSmallerAsTimePasses()
        {
            // arrange 
            var clock = new FakeSystemClock();
            var reservation = new Reservation(
                clock: clock,
                limiter: default,
                ok: true,
                timeToAct: clock.UtcNow.Add(TimeSpan.FromMinutes(5)),
                limit: default);

            // act
            var delay1 = reservation.Delay();
            clock.Advance(TimeSpan.FromMinutes(3));
            var delay2 = reservation.Delay();
            clock.Advance(TimeSpan.FromMinutes(3));
            var delay3 = reservation.Delay();

            // assert
            delay1.ShouldBe(TimeSpan.FromMinutes(5));
            delay2.ShouldBe(TimeSpan.FromMinutes(2));
            delay3.ShouldBe(TimeSpan.Zero);
        }

        [TestMethod]
        public void DelayFromNotChangedByTimePassing()
        {
            // arrange 
            var clock = new FakeSystemClock();
            var reservation = new Reservation(
                clock: clock,
                limiter: default,
                ok: true,
                timeToAct: clock.UtcNow.Add(TimeSpan.FromMinutes(5)),
                limit: default);

            var twoMinutesPast = clock.UtcNow.Subtract(TimeSpan.FromMinutes(2));
            var twoMinutesFuture = clock.UtcNow.Add(TimeSpan.FromMinutes(2));

            // act
            var delay1 = reservation.DelayFrom(clock.UtcNow);
            var delayPast1 = reservation.DelayFrom(twoMinutesPast);
            var delayFuture1 = reservation.DelayFrom(twoMinutesFuture);
            clock.Advance(TimeSpan.FromMinutes(3));
            var delay2 = reservation.DelayFrom(clock.UtcNow);
            var delayPast2 = reservation.DelayFrom(twoMinutesPast);
            var delayFuture2 = reservation.DelayFrom(twoMinutesFuture);

            // assert
            delay1.ShouldBe(TimeSpan.FromMinutes(5));
            delayPast1.ShouldBe(TimeSpan.FromMinutes(7));
            delayFuture1.ShouldBe(TimeSpan.FromMinutes(3));
            delay2.ShouldBe(TimeSpan.FromMinutes(2));
            delayPast2.ShouldBe(TimeSpan.FromMinutes(7));
            delayFuture2.ShouldBe(TimeSpan.FromMinutes(3));
        }
    }
}

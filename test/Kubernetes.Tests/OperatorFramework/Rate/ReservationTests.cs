// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Controller.Rate;
using Yarp.Kubernetes.OperatorFramework.Fakes;
using System;
using Xunit;

namespace Yarp.Kubernetes.OperatorFramework.Rate;

public class ReservationTests
{
    [Fact]
    public void NotOkayAlwaysReturnsMaxValueDelay()
    {
        var clock = new FakeSystemClock();
        var reservation = new Reservation(
            clock: clock,
            limiter: default,
            ok: false);

        var delay1 = reservation.Delay();
        var delayFrom1 = reservation.DelayFrom(clock.UtcNow);
        clock.Advance(TimeSpan.FromMinutes(3));
        var delay2 = reservation.Delay();
        var delayFrom2 = reservation.DelayFrom(clock.UtcNow);

        Assert.Equal(TimeSpan.MaxValue, delay1);
        Assert.Equal(TimeSpan.MaxValue, delayFrom1);
        Assert.Equal(TimeSpan.MaxValue, delay2);
        Assert.Equal(TimeSpan.MaxValue, delayFrom2);
    }

    [Fact]
    public void DelayIsZeroWhenTimeToActIsNowOrEarlier()
    {
        var clock = new FakeSystemClock();
        var reservation = new Reservation(
            clock: clock,
            limiter: default,
            ok: true,
            timeToAct: clock.UtcNow,
            limit: default);

        var delay1 = reservation.Delay();
        var delayFrom1 = reservation.DelayFrom(clock.UtcNow);
        clock.Advance(TimeSpan.FromMinutes(3));
        var delay2 = reservation.Delay();
        var delayFrom2 = reservation.DelayFrom(clock.UtcNow);

        Assert.Equal(TimeSpan.Zero, delay1);
        Assert.Equal(TimeSpan.Zero, delayFrom1);
        Assert.Equal(TimeSpan.Zero, delay2);
        Assert.Equal(TimeSpan.Zero, delayFrom2);
    }

    [Fact]
    public void DelayGetsSmallerAsTimePasses()
    {
        var clock = new FakeSystemClock();
        var reservation = new Reservation(
            clock: clock,
            limiter: default,
            ok: true,
            timeToAct: clock.UtcNow.Add(TimeSpan.FromMinutes(5)),
            limit: default);

        var delay1 = reservation.Delay();
        clock.Advance(TimeSpan.FromMinutes(3));
        var delay2 = reservation.Delay();
        clock.Advance(TimeSpan.FromMinutes(3));
        var delay3 = reservation.Delay();

        Assert.Equal(TimeSpan.FromMinutes(5), delay1);
        Assert.Equal(TimeSpan.FromMinutes(2), delay2);
        Assert.Equal(TimeSpan.Zero, delay3);
    }

    [Fact]
    public void DelayFromNotChangedByTimePassing()
    {
        var clock = new FakeSystemClock();
        var reservation = new Reservation(
            clock: clock,
            limiter: default,
            ok: true,
            timeToAct: clock.UtcNow.Add(TimeSpan.FromMinutes(5)),
            limit: default);

        var twoMinutesPast = clock.UtcNow.Subtract(TimeSpan.FromMinutes(2));
        var twoMinutesFuture = clock.UtcNow.Add(TimeSpan.FromMinutes(2));

        var delay1 = reservation.DelayFrom(clock.UtcNow);
        var delayPast1 = reservation.DelayFrom(twoMinutesPast);
        var delayFuture1 = reservation.DelayFrom(twoMinutesFuture);
        clock.Advance(TimeSpan.FromMinutes(3));
        var delay2 = reservation.DelayFrom(clock.UtcNow);
        var delayPast2 = reservation.DelayFrom(twoMinutesPast);
        var delayFuture2 = reservation.DelayFrom(twoMinutesFuture);

        Assert.Equal(TimeSpan.FromMinutes(5), delay1);
        Assert.Equal(TimeSpan.FromMinutes(7), delayPast1);
        Assert.Equal(TimeSpan.FromMinutes(3), delayFuture1);
        Assert.Equal(TimeSpan.FromMinutes(2), delay2);
        Assert.Equal(TimeSpan.FromMinutes(7), delayPast2);
        Assert.Equal(TimeSpan.FromMinutes(3), delayFuture2);
    }
}

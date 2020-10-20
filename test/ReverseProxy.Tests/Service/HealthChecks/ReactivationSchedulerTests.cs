// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;
using Xunit;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    public class ReactivationSchedulerTests
    {
        [Fact]
        public async Task Schedule_ReactivationPeriodElapsed_SetPassiveHealthToUnknown()
        {
            var destination = new DestinationInfo("destination0");
            destination.DynamicState = new DestinationDynamicState(new CompositeDestinationHealth(DestinationHealth.Unhealthy, DestinationHealth.Unhealthy));
            var clock = new UptimeClockStub() { TickCount = 1000 };
            var scheduler = new ReactivationScheduler(clock);

            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Passive);

            var reactivationPeriod = TimeSpan.FromSeconds(2);
            scheduler.Schedule(destination, reactivationPeriod);

            clock.TickCount += (long)reactivationPeriod.TotalMilliseconds;
            await Task.Delay(reactivationPeriod.Add(TimeSpan.FromMilliseconds(200))).ConfigureAwait(false);

            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Unknown, destination.DynamicState.Health.Passive);
        }

        [Fact]
        public async Task Schedule_ReactivationPeriodElapsedTwice_ReactivateDestinationOnlyOnce()
        {
            var destination = new DestinationInfo("destination0");
            destination.DynamicState = new DestinationDynamicState(new CompositeDestinationHealth(DestinationHealth.Unhealthy, DestinationHealth.Unhealthy));
            var clock = new UptimeClockStub() { TickCount = 1000 };
            var scheduler = new ReactivationScheduler(clock);

            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Passive);

            var reactivationPeriod = TimeSpan.FromSeconds(2);
            scheduler.Schedule(destination, reactivationPeriod);

            clock.TickCount += (long)reactivationPeriod.TotalMilliseconds;
            await Task.Delay(reactivationPeriod.Add(TimeSpan.FromMilliseconds(200))).ConfigureAwait(false);

            Assert.Equal(DestinationHealth.Unknown, destination.DynamicState.Health.Passive);

            // Set back to Unhealthy
            destination.DynamicStateSignal.Value = new DestinationDynamicState(destination.DynamicState.Health.ChangePassive(DestinationHealth.Unhealthy));

            clock.TickCount += (long)reactivationPeriod.TotalMilliseconds;
            await Task.Delay(reactivationPeriod.Add(TimeSpan.FromMilliseconds(200))).ConfigureAwait(false);

            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Passive);
        }

        [Fact]
        public async Task Schedule_TimerFiredButReactivationPeriodIsNotElapsed_DoNothing()
        {
            var destination = new DestinationInfo("destination0");
            destination.DynamicState = new DestinationDynamicState(new CompositeDestinationHealth(DestinationHealth.Unhealthy, DestinationHealth.Unhealthy));
            var clock = new UptimeClockStub() { TickCount = 1000 };
            var scheduler = new ReactivationScheduler(clock);

            var reactivationPeriod = TimeSpan.FromSeconds(2);
            scheduler.Schedule(destination, reactivationPeriod);

            // 'Now' moment has shifted to the future by only 1 second.
            clock.TickCount += 1000;
            await Task.Delay(reactivationPeriod.Add(TimeSpan.FromMilliseconds(200))).ConfigureAwait(false);

            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Passive);
        }

        private class UptimeClockStub : IUptimeClock
        {
            public long TickCount { get; set; }
        }
    }
}

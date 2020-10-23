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
            var scheduler = new ReactivationScheduler();

            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Passive);

            var reactivationPeriod = TimeSpan.FromSeconds(2);
            scheduler.Schedule(destination, reactivationPeriod);

            await Task.Delay(reactivationPeriod.Add(TimeSpan.FromMilliseconds(200))).ConfigureAwait(false);

            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Unknown, destination.DynamicState.Health.Passive);
        }

        [Fact]
        public async Task Schedule_ReactivationPeriodElapsedTwice_ReactivateDestinationOnlyOnce()
        {
            var destination = new DestinationInfo("destination0");
            destination.DynamicState = new DestinationDynamicState(new CompositeDestinationHealth(DestinationHealth.Unhealthy, DestinationHealth.Unhealthy));
            var scheduler = new ReactivationScheduler();

            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Passive);

            var reactivationPeriod = TimeSpan.FromSeconds(2);
            scheduler.Schedule(destination, reactivationPeriod);

            await Task.Delay(reactivationPeriod.Add(TimeSpan.FromMilliseconds(200))).ConfigureAwait(false);

            Assert.Equal(DestinationHealth.Unknown, destination.DynamicState.Health.Passive);

            // Set back to Unhealthy
            destination.DynamicStateSignal.Value = new DestinationDynamicState(destination.DynamicState.Health.ChangePassive(DestinationHealth.Unhealthy));

            await Task.Delay(reactivationPeriod.Add(TimeSpan.FromMilliseconds(200))).ConfigureAwait(false);

            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, destination.DynamicState.Health.Passive);
        }
    }
}

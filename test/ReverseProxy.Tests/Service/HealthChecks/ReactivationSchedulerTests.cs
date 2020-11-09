// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    public class ReactivationSchedulerTests
    {
        [Fact]
        public void Schedule_ReactivationPeriodElapsed_SetPassiveHealthToUnknown()
        {
            var destination = new DestinationInfo("destination0");
            destination.Health.Active = DestinationHealth.Unhealthy;
            destination.Health.Passive = DestinationHealth.Unhealthy;
            using var timerFactory = new TestTimerFactory();
            var scheduler = new ReactivationScheduler(timerFactory, new Mock<ILogger<ReactivationScheduler>>().Object);

            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Passive);

            var reactivationPeriod = TimeSpan.FromSeconds(2);
            scheduler.Schedule(destination, reactivationPeriod);
            timerFactory.VerifyTimer(0, 2000);

            timerFactory.FireAll();

            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Active);
            Assert.Equal(DestinationHealth.Unknown, destination.Health.Passive);
            timerFactory.AssertTimerDisposed(0);
        }

        [Fact]
        public void Schedule_DestinationIsAlreadyHealthy_DoNothing()
        {
            var destination = new DestinationInfo("destination0");
            destination.Health.Active = DestinationHealth.Unhealthy;
            destination.Health.Passive = DestinationHealth.Unhealthy;
            using var timerFactory = new TestTimerFactory();
            var scheduler = new ReactivationScheduler(timerFactory, new Mock<ILogger<ReactivationScheduler>>().Object);

            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Passive);

            scheduler.Schedule(destination, TimeSpan.FromSeconds(2));

            destination.Health.Passive = DestinationHealth.Healthy;

            timerFactory.FireAll();

            Assert.Equal(DestinationHealth.Healthy, destination.Health.Passive);
            timerFactory.AssertTimerDisposed(0);
        }
    }
}

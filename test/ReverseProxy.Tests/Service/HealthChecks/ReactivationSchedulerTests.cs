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

            timerFactory.FireAndWaitAll();

            timerFactory.VerifyTimer(0, 2000);
            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Active);
            Assert.Equal(DestinationHealth.Unknown, destination.Health.Passive);
        }

        [Fact]
        public void Schedule_ReactivationPeriodElapsedTwice_ReactivateDestinationOnlyOnce()
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

            timerFactory.FireAndWaitAll();

            timerFactory.VerifyTimer(0, 2000);
            Assert.Equal(1, timerFactory.Count);
            Assert.Equal(DestinationHealth.Unknown, destination.Health.Passive);
            Assert.Throws<ObjectDisposedException>(() => timerFactory.FireTimer(0));
        }
    }
}

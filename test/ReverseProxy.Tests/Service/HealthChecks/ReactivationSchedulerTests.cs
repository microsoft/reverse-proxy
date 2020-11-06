// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
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
            var destinationManager = new Mock<IDestinationManager>();
            destinationManager.SetupGet(m => m.Items).Returns(new[] { destination });
            var cluster = new ClusterInfo("cluster0", destinationManager.Object);
            using var timerFactory = new TestTimerFactory();
            var scheduler = new ReactivationScheduler(timerFactory, new Mock<ILogger<ReactivationScheduler>>().Object);

            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Passive);
            Assert.Empty(cluster.DynamicState.HealthyDestinations);

            var reactivationPeriod = TimeSpan.FromSeconds(2);
            scheduler.Schedule(cluster, destination, reactivationPeriod);
            timerFactory.VerifyTimer(0, 2000);

            timerFactory.FireAll();

            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Active);
            Assert.Equal(DestinationHealth.Unknown, destination.Health.Passive);
            Assert.Equal(1, cluster.DynamicState.HealthyDestinations.Count);
            Assert.Same(destination, cluster.DynamicState.HealthyDestinations[0]);
            timerFactory.AssertTimerDisposed(0);
        }

        [Fact]
        public void Schedule_DestinationIsAlreadyHealthy_DoNothing()
        {
            var destination = new DestinationInfo("destination0");
            destination.Health.Active = DestinationHealth.Unhealthy;
            destination.Health.Passive = DestinationHealth.Unhealthy;
            var destinationManager = new Mock<IDestinationManager>();
            var cluster = new ClusterInfo("cluster0", destinationManager.Object);
            using var timerFactory = new TestTimerFactory();
            var scheduler = new ReactivationScheduler(timerFactory, new Mock<ILogger<ReactivationScheduler>>().Object);

            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Passive);

            scheduler.Schedule(cluster, destination, TimeSpan.FromSeconds(2));

            destination.Health.Passive = DestinationHealth.Healthy;

            timerFactory.FireAll();

            Assert.Equal(DestinationHealth.Healthy, destination.Health.Passive);
            timerFactory.AssertTimerDisposed(0);
            destinationManager.VerifyNoOtherCalls();
        }
    }
}

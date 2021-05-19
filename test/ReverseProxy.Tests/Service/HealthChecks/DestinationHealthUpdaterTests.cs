// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service.Management;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    public class DestinationHealthUpdaterTests
    {
        [Fact]
        public async Task SetPassiveAsync_DestinationBecameUnhealthy_SetUnhealthyAndScheduleReactivation()
        {
            var destination = new DestinationState("destination0");
            destination.Health.Active = DestinationHealth.Healthy;
            destination.Health.Passive = DestinationHealth.Healthy;
            var cluster = CreateCluster(passive: true, active: false, destination);
            using var timerFactory = new TestTimerFactory();
            var updater = new DestinationHealthUpdater(timerFactory, new Mock<ILogger<DestinationHealthUpdater>>().Object);

            await updater.SetPassiveAsync(cluster, destination, DestinationHealth.Unhealthy, TimeSpan.FromSeconds(2));

            timerFactory.VerifyTimer(0, 2000);
            Assert.Empty(cluster.DynamicState.HealthyDestinations);
            Assert.Equal(DestinationHealth.Healthy, destination.Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Passive);

            timerFactory.FireAll();

            Assert.Equal(DestinationHealth.Healthy, destination.Health.Active);
            Assert.Equal(DestinationHealth.Unknown, destination.Health.Passive);
            Assert.Equal(1, cluster.DynamicState.HealthyDestinations.Count);
            Assert.Same(destination, cluster.DynamicState.HealthyDestinations[0]);
            timerFactory.AssertTimerDisposed(0);
        }

        [Fact]
        public async Task SetPassiveAsync_DestinationBecameHealthy_SetNewState()
        {
            var destination = new DestinationState("destination0");
            destination.Health.Active = DestinationHealth.Healthy;
            destination.Health.Passive = DestinationHealth.Unhealthy;
            var cluster = CreateCluster(passive: true, active: false, destination);
            using var timerFactory = new TestTimerFactory();
            var updater = new DestinationHealthUpdater(timerFactory, new Mock<ILogger<DestinationHealthUpdater>>().Object);

            await updater.SetPassiveAsync(cluster, destination, DestinationHealth.Healthy, TimeSpan.FromSeconds(2));

            Assert.Equal(0, timerFactory.Count);
            Assert.Equal(DestinationHealth.Healthy, destination.Health.Active);
            Assert.Equal(DestinationHealth.Healthy, destination.Health.Passive);
            Assert.Equal(1, cluster.DynamicState.HealthyDestinations.Count);
            Assert.Same(destination, cluster.DynamicState.HealthyDestinations[0]);
        }

        [Theory]
        [InlineData(DestinationHealth.Unhealthy)]
        [InlineData(DestinationHealth.Healthy)]
        [InlineData(DestinationHealth.Unknown)]
        public async Task SetPassiveAsync_HealthSateIsNotChanged_DoNothing(DestinationHealth health)
        {
            var destination = new DestinationState("destination0");
            destination.Health.Active = DestinationHealth.Healthy;
            destination.Health.Passive = health;
            var cluster = CreateCluster(passive: true, active: false, destination);
            using var timerFactory = new TestTimerFactory();
            var updater = new DestinationHealthUpdater(timerFactory, new Mock<ILogger<DestinationHealthUpdater>>().Object);

            await updater.SetPassiveAsync(cluster, destination, health, TimeSpan.FromSeconds(2));

            Assert.Equal(0, timerFactory.Count);
            Assert.Equal(DestinationHealth.Healthy, destination.Health.Active);
            Assert.Equal(health, destination.Health.Passive);
        }

        [Fact]
        public void SetActive_ChangedAndUnchangedHealthStates_SetChangedStates()
        {
            var destination0 = new DestinationState("destination0");
            destination0.Health.Active = DestinationHealth.Healthy;
            destination0.Health.Passive = DestinationHealth.Healthy;
            var destination1 = new DestinationState("destination1");
            destination1.Health.Active = DestinationHealth.Healthy;
            destination1.Health.Passive = DestinationHealth.Healthy;
            var destination2 = new DestinationState("destination2");
            destination2.Health.Active = DestinationHealth.Unhealthy;
            destination2.Health.Passive = DestinationHealth.Healthy;
            var destination3 = new DestinationState("destination3");
            destination3.Health.Active = DestinationHealth.Unhealthy;
            destination3.Health.Passive = DestinationHealth.Healthy;
            var cluster = CreateCluster(passive: false, active: true, destination0, destination1, destination2, destination3);
            var updater = new DestinationHealthUpdater(new Mock<ITimerFactory>().Object, new Mock<ILogger<DestinationHealthUpdater>>().Object);

            var newHealthStates = new[] {
                new NewActiveDestinationHealth(destination0, DestinationHealth.Unhealthy), new NewActiveDestinationHealth(destination1, DestinationHealth.Healthy),
                new NewActiveDestinationHealth(destination2, DestinationHealth.Unhealthy), new NewActiveDestinationHealth(destination3, DestinationHealth.Healthy)
            };
            updater.SetActive(cluster, newHealthStates);

            foreach(var newHealthState in newHealthStates)
            {
                Assert.Equal(newHealthState.NewActiveHealth, newHealthState.Destination.Health.Active);
                Assert.Equal(DestinationHealth.Healthy, newHealthState.Destination.Health.Passive);
            }

            Assert.Equal(2, cluster.DynamicState.HealthyDestinations.Count);
            Assert.Contains(cluster.DynamicState.HealthyDestinations, d => d == destination1);
            Assert.Contains(cluster.DynamicState.HealthyDestinations, d => d == destination3);
        }

        private static ClusterState CreateCluster(bool passive, bool active, params DestinationState[] destinations)
        {
            var cluster = new ClusterState("cluster0");
            cluster.Model = new ClusterModel(
                new ClusterConfig
                {
                    ClusterId = cluster.ClusterId,
                    HealthCheck = new HealthCheckConfig()
                    {
                        Passive = new PassiveHealthCheckConfig()
                        {
                            Policy = "policy0",
                            Enabled = passive,
                        },
                        Active = new ActiveHealthCheckConfig()
                        {
                            Enabled = active,
                            Policy = "policy1",
                        },
                    },
                },
                new HttpMessageInvoker(new HttpClientHandler()));

            foreach (var destination in destinations)
            {
                cluster.Destinations.TryAdd(destination.DestinationId, destination);
            }

            cluster.ProcessDestinationChanges();

            return cluster;
        }
    }
}

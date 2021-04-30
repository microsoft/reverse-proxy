// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Abstractions;

namespace Yarp.ReverseProxy.RuntimeModel.Tests
{
    public class ClusterInfoTests
    {
        [Fact]
        public void DynamicState_WithoutHealthChecks_AssumesAllHealthy()
        {
            var cluster = new ClusterState("abc");
            var destination1 = cluster.Destinations.GetOrAdd("d1", id => new DestinationInfo(id) { Health = { Active = DestinationHealth.Healthy } });
            var destination2 = cluster.Destinations.GetOrAdd("d2", id => new DestinationInfo(id) { Health = { Active = DestinationHealth.Unhealthy } });
            var destination3 = cluster.Destinations.GetOrAdd("d3", id => new DestinationInfo(id)); // Unknown health state
            var destination4 = cluster.Destinations.GetOrAdd("d4", id => new DestinationInfo(id) { Health = { Passive = DestinationHealth.Healthy } });
            cluster.ProcessDestinationChanges();

            var sorted = cluster.DynamicState.AllDestinations.OrderBy(d => d.DestinationId).ToList();
            Assert.Same(destination1, sorted[0]);
            Assert.Same(destination2, sorted[1]);
            Assert.Same(destination3, sorted[2]);
            Assert.Same(destination4, sorted[3]);

            sorted = cluster.DynamicState.HealthyDestinations.OrderBy(d => d.DestinationId).ToList();
            Assert.Same(destination1, sorted[0]);
            Assert.Same(destination2, sorted[1]);
            Assert.Same(destination3, sorted[2]);
            Assert.Same(destination4, sorted[3]);
        }

        [Fact]
        public void DynamicState_WithHealthChecks_HonorsHealthState()
        {
            var cluster = new ClusterState("abc");
            EnableHealthChecks(cluster);
            var destination1 = cluster.Destinations.GetOrAdd("d1", id => new DestinationInfo(id) { Health = { Active = DestinationHealth.Healthy } });
            var destination2 = cluster.Destinations.GetOrAdd("d2", id => new DestinationInfo(id) { Health = { Active = DestinationHealth.Unhealthy } });
            var destination3 = cluster.Destinations.GetOrAdd("d3", id => new DestinationInfo(id)); // Unknown health state
            var destination4 = cluster.Destinations.GetOrAdd("d4", id => new DestinationInfo(id) { Health = { Passive = DestinationHealth.Healthy } });
            var destination5 = cluster.Destinations.GetOrAdd("d5", id => new DestinationInfo(id) { Health = { Passive = DestinationHealth.Unhealthy } });
            cluster.ProcessDestinationChanges();

            Assert.Equal(5, cluster.DynamicState.AllDestinations.Count);
            var sorted = cluster.DynamicState.AllDestinations.OrderBy(d => d.DestinationId).ToList();
            Assert.Same(destination1, sorted[0]);
            Assert.Same(destination2, sorted[1]);
            Assert.Same(destination3, sorted[2]);
            Assert.Same(destination4, sorted[3]);
            Assert.Same(destination5, sorted[4]);

            Assert.Equal(3, cluster.DynamicState.HealthyDestinations.Count);
            sorted = cluster.DynamicState.HealthyDestinations.OrderBy(d => d.DestinationId).ToList();
            Assert.Same(destination1, sorted[0]);
            Assert.Same(destination3, sorted[1]);
            Assert.Same(destination4, sorted[2]);
        }

        // Verify that we detect changes to a cluster's ClusterInfo.Config
        [Fact]
        public void DynamicState_ManuallyUpdated()
        {
            var cluster = new ClusterState("abc");

            var state1 = cluster.DynamicState;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllDestinations);

            cluster.ProcessDestinationChanges();
            var state2 = cluster.DynamicState;
            Assert.NotSame(state1, state2);
            Assert.NotNull(state2);
            Assert.Empty(state2.AllDestinations);

            cluster.Config = new ClusterConfig(new Cluster(), httpClient: new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));
            Assert.Same(state2, cluster.DynamicState);

            cluster.UpdateDynamicState();
            Assert.NotSame(state2, cluster.DynamicState);
            Assert.Empty(cluster.DynamicState.AllDestinations);
        }

        // Verify that we detect addition / removal of a cluster's destination
        [Fact]
        public void DynamicState_ReactsToDestinationChanges()
        {
            var cluster = new ClusterState("abc");
            cluster.ProcessDestinationChanges();

            var state1 = cluster.DynamicState;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllDestinations);

            var destination = cluster.Destinations.GetOrAdd("d1", id => new DestinationInfo(id));
            cluster.ProcessDestinationChanges();
            Assert.NotSame(state1, cluster.DynamicState);
            var state2 = cluster.DynamicState;
            Assert.Contains(destination, state2.AllDestinations);

            cluster.Destinations.TryRemove("d1", out var _);
            cluster.ProcessDestinationChanges();
            Assert.NotSame(state2, cluster.DynamicState);
            var state3 = cluster.DynamicState;
            Assert.Empty(state3.AllDestinations);
        }

        // Verify that we detect dynamic state changes on a cluster's existing destinations
        [Fact]
        public void DynamicState_ReactsToDestinationStateChanges()
        {
            var cluster = new ClusterState("abc");
            EnableHealthChecks(cluster);
            cluster.ProcessDestinationChanges();

            var state1 = cluster.DynamicState;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllDestinations);

            var destination = cluster.Destinations.GetOrAdd("d1", id => new DestinationInfo(id));
            cluster.ProcessDestinationChanges();
            Assert.NotSame(state1, cluster.DynamicState);
            var state2 = cluster.DynamicState;

            destination.Health.Active = DestinationHealth.Unhealthy;
            cluster.UpdateDynamicState();
            Assert.NotSame(state2, cluster.DynamicState);
            var state3 = cluster.DynamicState;

            Assert.Contains(destination, state3.AllDestinations);
            Assert.Empty(state3.HealthyDestinations);

            destination.Health.Active = DestinationHealth.Healthy;
            cluster.UpdateDynamicState();
            Assert.NotSame(state3, cluster.DynamicState);
            var state4 = cluster.DynamicState;

            Assert.Contains(destination, state4.AllDestinations);
            Assert.Contains(destination, state4.HealthyDestinations);
        }

        private static void EnableHealthChecks(ClusterState cluster)
        {
            // Pretend that health checks are enabled so that destination health states are honored
            cluster.Config = new ClusterConfig(
                new Cluster
                {
                    HealthCheck = new HealthCheckOptions
                    {
                        Passive = new PassiveHealthCheckOptions
                        {
                            Enabled = true,
                            Policy = "FailureRate",
                            ReactivationPeriod = TimeSpan.FromMinutes(5),
                        },
                        Active = new ActiveHealthCheckOptions
                        {
                            Enabled = true,
                            Interval = TimeSpan.FromSeconds(5),
                            Timeout = TimeSpan.FromSeconds(30),
                            Policy = "Any5xxResponse",
                            Path = "/",
                        }
                    }
                },
                httpClient: new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));
        }
    }
}

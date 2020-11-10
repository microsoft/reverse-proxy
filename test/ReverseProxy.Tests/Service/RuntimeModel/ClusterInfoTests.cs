// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.Service.Management;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.RuntimeModel.Tests
{
    public class ClusterInfoTests
    {
        private static IClusterManager CreateClusterManager()
        {
            return new ClusterManager(new DestinationManagerFactory(), Array.Empty<IClusterChangeListener>());
        }

        [Fact]
        public void DynamicState_WithoutHealthChecks_AssumesAllHealthy()
        {
            var cluster = CreateClusterManager().GetOrCreateItem("abc", c => { });
            var destination1 = cluster.DestinationManager.GetOrCreateItem("d1", destination => destination.Health.Active = DestinationHealth.Healthy);
            var destination2 = cluster.DestinationManager.GetOrCreateItem("d2", destination => destination.Health.Active = DestinationHealth.Unhealthy);
            var destination3 = cluster.DestinationManager.GetOrCreateItem("d3", destination => { }); // Unknown health state
            var destination4 = cluster.DestinationManager.GetOrCreateItem("d4", destination => destination.Health.Passive = DestinationHealth.Healthy);
            cluster.UpdateDynamicState();

            Assert.Same(destination1, cluster.DynamicState.AllDestinations[0]);
            Assert.Same(destination2, cluster.DynamicState.AllDestinations[1]);
            Assert.Same(destination3, cluster.DynamicState.AllDestinations[2]);
            Assert.Same(destination4, cluster.DynamicState.AllDestinations[3]);

            Assert.Same(destination1, cluster.DynamicState.HealthyDestinations[0]);
            Assert.Same(destination2, cluster.DynamicState.HealthyDestinations[1]);
            Assert.Same(destination3, cluster.DynamicState.HealthyDestinations[2]);
            Assert.Same(destination4, cluster.DynamicState.HealthyDestinations[3]);
        }

        [Fact]
        public void DynamicState_WithHealthChecks_HonorsHealthState()
        {
            var cluster = CreateClusterManager().GetOrCreateItem("abc", c => EnableHealthChecks(c));
            var destination1 = cluster.DestinationManager.GetOrCreateItem("d1", destination => destination.Health.Active = DestinationHealth.Healthy);
            var destination2 = cluster.DestinationManager.GetOrCreateItem("d2", destination => destination.Health.Active = DestinationHealth.Unhealthy);
            var destination3 = cluster.DestinationManager.GetOrCreateItem("d3", destination => { }); // Unknown health state
            var destination4 = cluster.DestinationManager.GetOrCreateItem("d4", destination => destination.Health.Passive = DestinationHealth.Healthy);
            var destination5 = cluster.DestinationManager.GetOrCreateItem("d5", destination => destination.Health.Passive = DestinationHealth.Unhealthy);
            cluster.UpdateDynamicState();

            Assert.Equal(5, cluster.DynamicState.AllDestinations.Count);
            Assert.Same(destination1, cluster.DynamicState.AllDestinations[0]);
            Assert.Same(destination2, cluster.DynamicState.AllDestinations[1]);
            Assert.Same(destination3, cluster.DynamicState.AllDestinations[2]);
            Assert.Same(destination4, cluster.DynamicState.AllDestinations[3]);
            Assert.Same(destination5, cluster.DynamicState.AllDestinations[4]);

            Assert.Equal(3, cluster.DynamicState.HealthyDestinations.Count);
            Assert.Same(destination1, cluster.DynamicState.HealthyDestinations[0]);
            Assert.Same(destination3, cluster.DynamicState.HealthyDestinations[1]);
            Assert.Same(destination4, cluster.DynamicState.HealthyDestinations[2]);
        }

        // Verify that we detect changes to a cluster's ClusterInfo.Config
        [Fact]
        public void DynamicState_ManuallyUpdated()
        {
            var cluster = CreateClusterManager().GetOrCreateItem("abc", c => { });

            var state1 = cluster.DynamicState;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllDestinations);

            cluster.UpdateDynamicState();
            var state2 = cluster.DynamicState;
            Assert.NotSame(state1, state2);
            Assert.NotNull(state2);
            Assert.Empty(state2.AllDestinations);

            cluster.Config = new ClusterConfig(cluster: default, healthCheckOptions: default, loadBalancingOptions: default, sessionAffinityOptions: default,
                httpClient: new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object), httpClientOptions: default, httpRequestOptions: default, metadata: new Dictionary<string, string>());
            Assert.Same(state2, cluster.DynamicState);

            cluster.UpdateDynamicState();
            Assert.NotSame(state2, cluster.DynamicState);
            Assert.Empty(cluster.DynamicState.AllDestinations);
        }

        // Verify that we detect addition / removal of a cluster's destination
        [Fact]
        public void DynamicState_ReactsToDestinationChanges()
        {
            var cluster = CreateClusterManager().GetOrCreateItem("abc", c => { });
            cluster.UpdateDynamicState();

            var state1 = cluster.DynamicState;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllDestinations);

            var destination = cluster.DestinationManager.GetOrCreateItem("d1", destination => { });
            cluster.UpdateDynamicState();
            Assert.NotSame(state1, cluster.DynamicState);
            var state2 = cluster.DynamicState;
            Assert.Contains(destination, state2.AllDestinations);

            cluster.DestinationManager.TryRemoveItem("d1");
            cluster.UpdateDynamicState();
            Assert.NotSame(state2, cluster.DynamicState);
            var state3 = cluster.DynamicState;
            Assert.Empty(state3.AllDestinations);
        }

        // Verify that we detect dynamic state changes on a cluster's existing destinations
        [Fact]
        public void DynamicState_ReactsToDestinationStateChanges()
        {
            var cluster = CreateClusterManager().GetOrCreateItem("abc", c => EnableHealthChecks(c));
            cluster.UpdateDynamicState();

            var state1 = cluster.DynamicState;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllDestinations);

            var destination = cluster.DestinationManager.GetOrCreateItem("d1", destination => { });
            cluster.UpdateDynamicState();
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

        private static void EnableHealthChecks(ClusterInfo cluster)
        {
            // Pretend that health checks are enabled so that destination health states are honored
            cluster.Config = new ClusterConfig(
                new Cluster(),
                healthCheckOptions: new ClusterHealthCheckOptions(
                    new ClusterPassiveHealthCheckOptions(
                        enabled: true,
                        policy: "FailureRate",
                        reactivationPeriod: TimeSpan.FromMinutes(5)),
                    new ClusterActiveHealthCheckOptions(
                        enabled: true,
                        interval: TimeSpan.FromSeconds(5),
                        timeout: TimeSpan.FromSeconds(30),
                        policy: "Any5xxResponse",
                        path: "/")),
                loadBalancingOptions: default,
                sessionAffinityOptions: default,
                httpClient: new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object),
                httpClientOptions: default,
                httpRequestOptions: default,
                metadata: new Dictionary<string, string>());
        }
    }
}

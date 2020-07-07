// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.RuntimeModel.Tests
{
    public class ClusterInfoTests : TestAutoMockBase
    {
        private readonly IClusterManager _clusterManager;

        public ClusterInfoTests()
        {
            // These are satellite classes with simple functionality and adding the actual implementations
            // much more convenient than replicating functionality for the purpose of the tests.
            Provide<IDestinationManagerFactory, DestinationManagerFactory>();
            Provide<IProxyHttpClientFactoryFactory, ProxyHttpClientFactoryFactory>();
            _clusterManager = Provide<IClusterManager, ClusterManager>();
        }

        [Fact]
        public void DynamicState_WithoutHealthChecks_AssumesAllHealthy()
        {
            // Arrange
            var cluster = _clusterManager.GetOrCreateItem("abc", c => { });
            var destination1 = cluster.DestinationManager.GetOrCreateItem("d1", destination => destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Healthy));
            var destination2 = cluster.DestinationManager.GetOrCreateItem("d2", destination => destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Unhealthy));
            var destination3 = cluster.DestinationManager.GetOrCreateItem("d3", destination => destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Unknown));
            var destination4 = cluster.DestinationManager.GetOrCreateItem("d4", destination => destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Healthy));

            // Assert
            Assert.Same(destination1, cluster.DynamicState.Value.AllDestinations[0]);
            Assert.Same(destination2, cluster.DynamicState.Value.AllDestinations[1]);
            Assert.Same(destination3, cluster.DynamicState.Value.AllDestinations[2]);
            Assert.Same(destination4, cluster.DynamicState.Value.AllDestinations[3]);

            Assert.Same(destination1, cluster.DynamicState.Value.HealthyDestinations[0]);
            Assert.Same(destination2, cluster.DynamicState.Value.HealthyDestinations[1]);
            Assert.Same(destination3, cluster.DynamicState.Value.HealthyDestinations[2]);
            Assert.Same(destination4, cluster.DynamicState.Value.HealthyDestinations[3]);
        }

        [Fact]
        public void DynamicState_WithHealthChecks_HonorsHealthState()
        {
            // Arrange
            var cluster = _clusterManager.GetOrCreateItem("abc", c => EnableHealthChecks(c));
            var destination1 = cluster.DestinationManager.GetOrCreateItem("d1", destination => destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Healthy));
            var destination2 = cluster.DestinationManager.GetOrCreateItem("d2", destination => destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Unhealthy));
            var destination3 = cluster.DestinationManager.GetOrCreateItem("d3", destination => destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Unknown));
            var destination4 = cluster.DestinationManager.GetOrCreateItem("d4", destination => destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Healthy));

            // Assert
            Assert.Same(destination1, cluster.DynamicState.Value.AllDestinations[0]);
            Assert.Same(destination2, cluster.DynamicState.Value.AllDestinations[1]);
            Assert.Same(destination3, cluster.DynamicState.Value.AllDestinations[2]);
            Assert.Same(destination4, cluster.DynamicState.Value.AllDestinations[3]);

            Assert.Same(destination1, cluster.DynamicState.Value.HealthyDestinations[0]);
            Assert.Same(destination4, cluster.DynamicState.Value.HealthyDestinations[1]);
        }

        // Verify that we detect changes to a cluster's ClusterInfo.Config
        [Fact]
        public void DynamicState_ReactsToClusterConfigChanges()
        {
            // Arrange
            var cluster = _clusterManager.GetOrCreateItem("abc", c => { });

            // Act & Assert
            var state1 = cluster.DynamicState.Value;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllDestinations);

            cluster.Config.Value = new ClusterConfig(healthCheckOptions: default, loadBalancingOptions: default, sessionAffinityOptions: default);
            Assert.NotSame(state1, cluster.DynamicState.Value);
            Assert.Empty(cluster.DynamicState.Value.AllDestinations);
        }

        // Verify that we detect addition / removal of a cluster's destination
        [Fact]
        public void DynamicState_ReactsToDestinationChanges()
        {
            // Arrange
            var cluster = _clusterManager.GetOrCreateItem("abc", c => { });

            // Act & Assert
            var state1 = cluster.DynamicState.Value;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllDestinations);

            var destination = cluster.DestinationManager.GetOrCreateItem("d1", destination => { });
            Assert.NotSame(state1, cluster.DynamicState.Value);
            var state2 = cluster.DynamicState.Value;
            Assert.Contains(destination, state2.AllDestinations);

            cluster.DestinationManager.TryRemoveItem("d1");
            Assert.NotSame(state2, cluster.DynamicState.Value);
            var state3 = cluster.DynamicState.Value;
            Assert.Empty(state3.AllDestinations);
        }

        // Verify that we detect dynamic state changes on a cluster's existing destinations
        [Fact]
        public void DynamicState_ReactsToDestinationStateChanges()
        {
            // Arrange
            var cluster = _clusterManager.GetOrCreateItem("abc", c => EnableHealthChecks(c));

            // Act & Assert
            var state1 = cluster.DynamicState.Value;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllDestinations);

            var destination = cluster.DestinationManager.GetOrCreateItem("d1", destination => { });
            Assert.NotSame(state1, cluster.DynamicState.Value);
            var state2 = cluster.DynamicState.Value;

            destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Unhealthy);
            Assert.NotSame(state2, cluster.DynamicState.Value);
            var state3 = cluster.DynamicState.Value;

            Assert.Contains(destination, state3.AllDestinations);
            Assert.Empty(state3.HealthyDestinations);

            destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Healthy);
            Assert.NotSame(state3, cluster.DynamicState.Value);
            var state4 = cluster.DynamicState.Value;

            Assert.Contains(destination, state4.AllDestinations);
            Assert.Contains(destination, state4.HealthyDestinations);
        }

        private static void EnableHealthChecks(ClusterInfo cluster)
        {
            // Pretend that health checks are enabled so that destination health states are honored
            cluster.Config.Value = new ClusterConfig(
                healthCheckOptions: new ClusterConfig.ClusterHealthCheckOptions(
                    enabled: true,
                    interval: TimeSpan.FromSeconds(5),
                    timeout: TimeSpan.FromSeconds(30),
                    port: 30000,
                    path: "/"),
                loadBalancingOptions: default,
                sessionAffinityOptions: default);
        }
    }
}

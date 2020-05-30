// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.RuntimeModel.Tests
{
    public class BackendInfoTests : TestAutoMockBase
    {
        private readonly IBackendManager _backendManager;

        public BackendInfoTests()
        {
            // These are satellite classes with simple functionality and adding the actual implementations
            // much more convenient than replicating functionality for the purpose of the tests.
            Provide<IDestinationManagerFactory, DestinationManagerFactory>();
            Provide<IProxyHttpClientFactoryFactory, ProxyHttpClientFactoryFactory>();
            _backendManager = Provide<IBackendManager, BackendManager>();
        }

        [Fact]
        public void DynamicState_WithoutHealthChecks_AssumesAllHealthy()
        {
            // Arrange
            var backend = _backendManager.GetOrCreateItem("abc", backend => { });
            var destination1 = backend.DestinationManager.GetOrCreateItem("d1", destination => destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Healthy));
            var destination2 = backend.DestinationManager.GetOrCreateItem("d2", destination => destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Unhealthy));
            var destination3 = backend.DestinationManager.GetOrCreateItem("d3", destination => destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Unknown));
            var destination4 = backend.DestinationManager.GetOrCreateItem("d4", destination => destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Healthy));

            // Assert
            Assert.Same(destination1, backend.DynamicState.Value.AllDestinations[0]);
            Assert.Same(destination2, backend.DynamicState.Value.AllDestinations[1]);
            Assert.Same(destination3, backend.DynamicState.Value.AllDestinations[2]);
            Assert.Same(destination4, backend.DynamicState.Value.AllDestinations[3]);

            Assert.Same(destination1, backend.DynamicState.Value.HealthyDestinations[0]);
            Assert.Same(destination2, backend.DynamicState.Value.HealthyDestinations[1]);
            Assert.Same(destination3, backend.DynamicState.Value.HealthyDestinations[2]);
            Assert.Same(destination4, backend.DynamicState.Value.HealthyDestinations[3]);
        }

        [Fact]
        public void DynamicState_WithHealthChecks_HonorsHealthState()
        {
            // Arrange
            var backend = _backendManager.GetOrCreateItem("abc", backend => EnableHealthChecks(backend));
            var destination1 = backend.DestinationManager.GetOrCreateItem("d1", destination => destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Healthy));
            var destination2 = backend.DestinationManager.GetOrCreateItem("d2", destination => destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Unhealthy));
            var destination3 = backend.DestinationManager.GetOrCreateItem("d3", destination => destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Unknown));
            var destination4 = backend.DestinationManager.GetOrCreateItem("d4", destination => destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Healthy));

            // Assert
            Assert.Same(destination1, backend.DynamicState.Value.AllDestinations[0]);
            Assert.Same(destination2, backend.DynamicState.Value.AllDestinations[1]);
            Assert.Same(destination3, backend.DynamicState.Value.AllDestinations[2]);
            Assert.Same(destination4, backend.DynamicState.Value.AllDestinations[3]);

            Assert.Same(destination1, backend.DynamicState.Value.HealthyDestinations[0]);
            Assert.Same(destination4, backend.DynamicState.Value.HealthyDestinations[1]);
        }

        // Verify that we detect changes to a backend's BackendInfo.Config
        [Fact]
        public void DynamicState_ReactsToBackendConfigChanges()
        {
            // Arrange
            var backend = _backendManager.GetOrCreateItem("abc", backend => { });

            // Act & Assert
            var state1 = backend.DynamicState.Value;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllDestinations);

            backend.Config.Value = new BackendConfig(healthCheckOptions: default, loadBalancingOptions: default);
            Assert.NotSame(state1, backend.DynamicState.Value);
            Assert.Empty(backend.DynamicState.Value.AllDestinations);
        }

        // Verify that we detect addition / removal of a backend's destination
        [Fact]
        public void DynamicState_ReactsToBackendEndpointChanges()
        {
            // Arrange
            var backend = _backendManager.GetOrCreateItem("abc", backend => { });

            // Act & Assert
            var state1 = backend.DynamicState.Value;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllDestinations);

            var destination = backend.DestinationManager.GetOrCreateItem("d1", destination => { });
            Assert.NotSame(state1, backend.DynamicState.Value);
            var state2 = backend.DynamicState.Value;
            Assert.Contains(destination, state2.AllDestinations);

            backend.DestinationManager.TryRemoveItem("d1");
            Assert.NotSame(state2, backend.DynamicState.Value);
            var state3 = backend.DynamicState.Value;
            Assert.Empty(state3.AllDestinations);
        }

        // Verify that we detect dynamic state changes on a backend's existing destinations
        [Fact]
        public void DynamicState_ReactsToBackendEndpointStateChanges()
        {
            // Arrange
            var backend = _backendManager.GetOrCreateItem("abc", backend => EnableHealthChecks(backend));

            // Act & Assert
            var state1 = backend.DynamicState.Value;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllDestinations);

            var destination = backend.DestinationManager.GetOrCreateItem("d1", destination => { });
            Assert.NotSame(state1, backend.DynamicState.Value);
            var state2 = backend.DynamicState.Value;

            destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Unhealthy);
            Assert.NotSame(state2, backend.DynamicState.Value);
            var state3 = backend.DynamicState.Value;

            Assert.Contains(destination, state3.AllDestinations);
            Assert.Empty(state3.HealthyDestinations);

            destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Healthy);
            Assert.NotSame(state3, backend.DynamicState.Value);
            var state4 = backend.DynamicState.Value;

            Assert.Contains(destination, state4.AllDestinations);
            Assert.Contains(destination, state4.HealthyDestinations);
        }

        private static void EnableHealthChecks(BackendInfo backend)
        {
            // Pretend that health checks are enabled so that destination health states are honored
            backend.Config.Value = new BackendConfig(
                healthCheckOptions: new BackendConfig.BackendHealthCheckOptions(
                    enabled: true,
                    interval: TimeSpan.FromSeconds(5),
                    timeout: TimeSpan.FromSeconds(30),
                    port: 30000,
                    path: "/"),
                loadBalancingOptions: default);
        }
    }
}

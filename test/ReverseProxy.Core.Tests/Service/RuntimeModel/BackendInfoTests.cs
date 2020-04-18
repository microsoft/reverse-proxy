// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Core.Service.Management;
using Microsoft.ReverseProxy.Core.Service.Proxy.Infra;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.RuntimeModel.Tests
{
    public class BackendInfoTests : TestAutoMockBase
    {
        private readonly IBackendManager _backendManager;

        public BackendInfoTests()
        {
            // These are satellite classes with simple functionality and adding the actual implementations
            // much more convenient than replicating functionality for the purpose of the tests.
            Provide<IEndpointManagerFactory, EndpointManagerFactory>();
            Provide<IProxyHttpClientFactoryFactory, ProxyHttpClientFactoryFactory>();
            _backendManager = Provide<IBackendManager, BackendManager>();
        }

        [Fact]
        public void DynamicState_WithoutHealthChecks_AssumesAllHealthy()
        {
            // Arrange
            var backend = _backendManager.GetOrCreateItem("abc", backend => { });
            var endpoint1 = backend.EndpointManager.GetOrCreateItem("ep1", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Healthy));
            var endpoint2 = backend.EndpointManager.GetOrCreateItem("ep2", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Unhealthy));
            var endpoint3 = backend.EndpointManager.GetOrCreateItem("ep3", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Unknown));
            var endpoint4 = backend.EndpointManager.GetOrCreateItem("ep4", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Healthy));

            // Assert
            Assert.Equal(endpoint1, backend.DynamicState.Value.AllEndpoints[0]);
            Assert.Equal(endpoint2, backend.DynamicState.Value.AllEndpoints[1]);
            Assert.Equal(endpoint3, backend.DynamicState.Value.AllEndpoints[2]);
            Assert.Equal(endpoint4, backend.DynamicState.Value.AllEndpoints[3]);

            Assert.Equal(endpoint1, backend.DynamicState.Value.HealthyEndpoints[0]);
            Assert.Equal(endpoint2, backend.DynamicState.Value.HealthyEndpoints[1]);
            Assert.Equal(endpoint3, backend.DynamicState.Value.HealthyEndpoints[2]);
            Assert.Equal(endpoint4, backend.DynamicState.Value.HealthyEndpoints[3]);
        }

        [Fact]
        public void DynamicState_WithHealthChecks_HonorsHealthState()
        {
            // Arrange
            var backend = _backendManager.GetOrCreateItem("abc", backend => EnableHealthChecks(backend));
            var endpoint1 = backend.EndpointManager.GetOrCreateItem("ep1", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Healthy));
            var endpoint2 = backend.EndpointManager.GetOrCreateItem("ep2", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Unhealthy));
            var endpoint3 = backend.EndpointManager.GetOrCreateItem("ep3", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Unknown));
            var endpoint4 = backend.EndpointManager.GetOrCreateItem("ep4", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Healthy));

            // Assert
            Assert.Equal(endpoint1, backend.DynamicState.Value.AllEndpoints[0]);
            Assert.Equal(endpoint2, backend.DynamicState.Value.AllEndpoints[1]);
            Assert.Equal(endpoint3, backend.DynamicState.Value.AllEndpoints[2]);
            Assert.Equal(endpoint4, backend.DynamicState.Value.AllEndpoints[3]);

            Assert.Equal(endpoint1, backend.DynamicState.Value.HealthyEndpoints[0]);
            Assert.Equal(endpoint4, backend.DynamicState.Value.HealthyEndpoints[1]);
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
            Assert.Empty(state1.AllEndpoints);

            backend.Config.Value = new BackendConfig(healthCheckOptions: default, loadBalancingOptions: default);
            Assert.NotEqual(state1, backend.DynamicState.Value);
            Assert.Empty(backend.DynamicState.Value.AllEndpoints);
        }

        // Verify that we detect addition / removal of a backend's endpoint
        [Fact]
        public void DynamicState_ReactsToBackendEndpointChanges()
        {
            // Arrange
            var backend = _backendManager.GetOrCreateItem("abc", backend => { });

            // Act & Assert
            var state1 = backend.DynamicState.Value;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllEndpoints);

            var endpoint = backend.EndpointManager.GetOrCreateItem("ep1", endpoint => { });
            Assert.NotEqual(state1, backend.DynamicState.Value);
            var state2 = backend.DynamicState.Value;
            Assert.Contains(endpoint, state2.AllEndpoints);

            backend.EndpointManager.TryRemoveItem("ep1");
            Assert.NotEqual(state2, backend.DynamicState.Value);
            var state3 = backend.DynamicState.Value;
            Assert.Empty(state3.AllEndpoints);
        }

        // Verify that we detect dynamic state changes on a backend's existing endpoints
        [Fact]
        public void DynamicState_ReactsToBackendEndpointStateChanges()
        {
            // Arrange
            var backend = _backendManager.GetOrCreateItem("abc", backend => EnableHealthChecks(backend));

            // Act & Assert
            var state1 = backend.DynamicState.Value;
            Assert.NotNull(state1);
            Assert.Empty(state1.AllEndpoints);

            var endpoint = backend.EndpointManager.GetOrCreateItem("ep1", endpoint => { });
            Assert.NotEqual(state1, backend.DynamicState.Value);
            var state2 = backend.DynamicState.Value;

            endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Unhealthy);
            Assert.NotEqual(state2, backend.DynamicState.Value);
            var state3 = backend.DynamicState.Value;

            Assert.Contains(endpoint, state3.AllEndpoints);
            Assert.Empty(state3.HealthyEndpoints);

            endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Healthy);
            Assert.NotEqual(state3, backend.DynamicState.Value);
            var state4 = backend.DynamicState.Value;

            Assert.Contains(endpoint, state4.AllEndpoints);
            Assert.Contains(endpoint, state4.HealthyEndpoints);
        }

        private static void EnableHealthChecks(BackendInfo backend)
        {
            // Pretend that health checks are enabled so that endpoint health states are honored
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

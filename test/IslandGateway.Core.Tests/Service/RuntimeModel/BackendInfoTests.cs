// <copyright file="BackendInfoTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using FluentAssertions;
using IslandGateway.Core.Service.Management;
using IslandGateway.Core.Service.Proxy.Infra;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.RuntimeModel.Tests
{
    public class BackendInfoTests : TestAutoMockBase
    {
        private readonly IBackendManager _backendManager;

        public BackendInfoTests()
        {
            // These are satellite classes with simple functionality and adding the actual implementations
            // much more convenient than replicating functionality for the purpose of the tests.
            this.Provide<IEndpointManagerFactory, EndpointManagerFactory>();
            this.Provide<IProxyHttpClientFactoryFactory, ProxyHttpClientFactoryFactory>();
            this._backendManager = this.Provide<IBackendManager, BackendManager>();
        }

        [Fact]
        public void DynamicState_WithoutHealthChecks_AssumesAllHealthy()
        {
            // Arrange
            var backend = this._backendManager.GetOrCreateItem("abc", backend => { });
            var endpoint1 = backend.EndpointManager.GetOrCreateItem("ep1", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Healthy));
            var endpoint2 = backend.EndpointManager.GetOrCreateItem("ep2", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Unhealthy));
            var endpoint3 = backend.EndpointManager.GetOrCreateItem("ep3", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Unknown));
            var endpoint4 = backend.EndpointManager.GetOrCreateItem("ep4", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Healthy));

            // Assert
            backend.DynamicState.Value.AllEndpoints.Should().BeEquivalentTo(endpoint1, endpoint2, endpoint3, endpoint4);
            backend.DynamicState.Value.HealthyEndpoints.Should().BeEquivalentTo(endpoint1, endpoint2, endpoint3, endpoint4);
        }

        [Fact]
        public void DynamicState_WithHealthChecks_HonorsHealthState()
        {
            // Arrange
            var backend = this._backendManager.GetOrCreateItem("abc", backend => EnableHealthChecks(backend));
            var endpoint1 = backend.EndpointManager.GetOrCreateItem("ep1", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Healthy));
            var endpoint2 = backend.EndpointManager.GetOrCreateItem("ep2", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Unhealthy));
            var endpoint3 = backend.EndpointManager.GetOrCreateItem("ep3", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Unknown));
            var endpoint4 = backend.EndpointManager.GetOrCreateItem("ep4", endpoint => endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Healthy));

            // Assert
            backend.DynamicState.Value.AllEndpoints.Should().BeEquivalentTo(endpoint1, endpoint2, endpoint3, endpoint4);
            backend.DynamicState.Value.HealthyEndpoints.Should().BeEquivalentTo(endpoint1, endpoint4);
        }

        // Verify that we detect changes to a backend's BackendInfo.Config
        [Fact]
        public void DynamicState_ReactsToBackendConfigChanges()
        {
            // Arrange
            var backend = this._backendManager.GetOrCreateItem("abc", backend => { });

            // Act & Assert
            var state1 = backend.DynamicState.Value;
            state1.Should().NotBeNull();
            state1.AllEndpoints.Should().BeEmpty();

            backend.Config.Value = new BackendConfig(healthCheckOptions: default, loadBalancingOptions: default);
            backend.DynamicState.Value.Should().NotBeSameAs(state1);
            backend.DynamicState.Value.AllEndpoints.Should().BeEmpty();
        }

        // Verify that we detect addition / removal of a backend's endpoint
        [Fact]
        public void DynamicState_ReactsToBackendEndpointChanges()
        {
            // Arrange
            var backend = this._backendManager.GetOrCreateItem("abc", backend => { });

            // Act & Assert
            var state1 = backend.DynamicState.Value;
            state1.Should().NotBeNull();
            state1.AllEndpoints.Should().BeEmpty();

            var endpoint = backend.EndpointManager.GetOrCreateItem("ep1", endpoint => { });
            backend.DynamicState.Value.Should().NotBeSameAs(state1);
            var state2 = backend.DynamicState.Value;
            state2.AllEndpoints.Should().BeEquivalentTo(endpoint);

            backend.EndpointManager.TryRemoveItem("ep1");
            backend.DynamicState.Value.Should().NotBeSameAs(state2);
            var state3 = backend.DynamicState.Value;
            state3.AllEndpoints.Should().BeEmpty();
        }

        // Verify that we detect dynamic state changes on a backend's existing endpoints
        [Fact]
        public void DynamicState_ReactsToBackendEndpointStateChanges()
        {
            // Arrange
            var backend = this._backendManager.GetOrCreateItem("abc", backend => EnableHealthChecks(backend));

            // Act & Assert
            var state1 = backend.DynamicState.Value;
            state1.Should().NotBeNull();
            state1.AllEndpoints.Should().BeEmpty();

            var endpoint = backend.EndpointManager.GetOrCreateItem("ep1", endpoint => { });
            backend.DynamicState.Value.Should().NotBeSameAs(state1);
            var state2 = backend.DynamicState.Value;

            endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Unhealthy);
            backend.DynamicState.Value.Should().NotBeSameAs(state2);
            var state3 = backend.DynamicState.Value;

            state3.AllEndpoints.Should().BeEquivalentTo(endpoint);
            state3.HealthyEndpoints.Should().BeEmpty();

            endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Healthy);
            backend.DynamicState.Value.Should().NotBeSameAs(state3);
            var state4 = backend.DynamicState.Value;

            state4.AllEndpoints.Should().BeEquivalentTo(endpoint);
            state4.HealthyEndpoints.Should().BeEquivalentTo(endpoint);
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

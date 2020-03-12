// <copyright file="HealthProbeWorkerTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Threading.Tasks;
using IslandGateway.Common.Abstractions.Time;
using IslandGateway.Common.Util;
using IslandGateway.Core.RuntimeModel;
using IslandGateway.Core.Service.Management;
using IslandGateway.Core.Service.Proxy.Infra;
using IslandGateway.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.Service.HealthProbe
{
    public class HealthProbeWorkerTests : TestAutoMockBase
    {
        // The prober class is used by the healthProbe class, here we would mock the prober since we only care about the behavior of healthProbe class.
        // And we also want to customize the behavior of prober so we could have the full control of the unit test.
        private Mock<IBackendProber> backendProber;
        private IBackendManager backendManager;
        private BackendConfig backendConfig;

        public HealthProbeWorkerTests()
        {
            // Set up all the parameter needed for healthProberWorker.
            this.Provide<IMonotonicTimer, MonotonicTimer>();
            this.Provide<ILogger, Logger<HealthProbeWorker>>();
            this.Mock<IProxyHttpClientFactoryFactory>()
                .Setup(f => f.CreateFactory())
                .Returns(new Mock<IProxyHttpClientFactory>().Object);

            // Set up backends. We are going to fake multiple service for us to probe.
            this.backendManager = this.Provide<IBackendManager, BackendManager>();
            this.backendConfig = new BackendConfig(
                healthCheckOptions: new BackendConfig.BackendHealthCheckOptions(
                    enabled: true,
                    interval: TimeSpan.FromSeconds(1),
                    timeout: TimeSpan.FromSeconds(1),
                    port: 8000,
                    path: "/example"),
                loadBalancingOptions: default);

            // Set up prober. We do not want to let prober really perform any actions.
            // The behavior of prober should be tested in its own unit test, see "BackendProberTests.cs".
            this.backendProber = new Mock<IBackendProber>();
            this.backendProber.Setup(p => p.Start(It.IsAny<AsyncSemaphore>()));
            this.backendProber.Setup(p => p.StopAsync());
            this.backendProber.Setup(p => p.BackendId).Returns("service0");
            this.backendProber.Setup(p => p.Config).Returns(this.backendConfig);
            this.Mock<IBackendProberFactory>()
                .Setup(
                    r => r.CreateBackendProber(
                        It.IsAny<string>(),
                        It.IsAny<BackendConfig>(),
                        It.IsAny<IEndpointManager>()))
                .Returns(this.backendProber.Object);
        }

        [Fact]
        public void Constructor_Works()
        {
            this.Create<HealthProbeWorker>();
        }

        [Fact]
        public async Task UpdateTrackedBackends_NoBackends_ShouldNotStartProber()
        {
            var health = this.Create<HealthProbeWorker>();
            await health.UpdateTrackedBackends();

            // There is no service, no prober should be created or started.
            this.backendProber.Verify(p => p.Start(It.IsAny<AsyncSemaphore>()), Times.Never());
        }

        [Fact]
        public async Task UpdateTrackedBackends_HasBackends_ShouldStartProber()
        {
            // Set up endpoints for probing, pretend that we have three replica.
            var endpointmanger = EndpointManagerGenerator(3);
            this.Mock<IEndpointManagerFactory>()
                .Setup(e => e.CreateEndpointManager())
                .Returns(endpointmanger);

            // Set up backends for probing, pretend that we have three services, each services have three replica.
            this.backendManager.GetOrCreateItem("service0", item => { item.Config.Value = this.backendConfig; });
            this.backendManager.GetOrCreateItem("service1", item => { item.Config.Value = this.backendConfig; });
            this.backendManager.GetOrCreateItem("service2", item => { item.Config.Value = this.backendConfig; });

            // Start probing.
            var health = this.Create<HealthProbeWorker>();
            await health.UpdateTrackedBackends();

            // There is three services, three prober should be created and started.
            this.backendProber.Verify(p => p.Start(It.IsAny<AsyncSemaphore>()), Times.Exactly(3));
        }

        [Fact]
        public async Task UpdateTrackedBackends_ProbeNotEnabled_ShouldNotStartProber()
        {
            // Set up endpoints for probing, pretend that we have three replica.
            var endpointmanger = EndpointManagerGenerator(3);
            this.Mock<IEndpointManagerFactory>()
                .Setup(e => e.CreateEndpointManager())
                .Returns(endpointmanger);

            // Set up backends for probing, pretend that we have three services, each services have three replica.
            this.backendManager.GetOrCreateItem(
                "service0",
                item =>
                {
                    item.Config.Value = new BackendConfig(
                        healthCheckOptions: new BackendConfig.BackendHealthCheckOptions(
                            enabled: false,
                            interval: TimeSpan.FromSeconds(5),
                            timeout: TimeSpan.FromSeconds(20),
                            port: 1234,
                            path: "/"),
                        loadBalancingOptions: default);
                });

            // Start probing.
            var health = this.Create<HealthProbeWorker>();
            await health.UpdateTrackedBackends();

            // Probing is disabled for this backend, no prober should be created or started.
            this.backendProber.Verify(p => p.Start(It.IsAny<AsyncSemaphore>()), Times.Never());
        }

        [Fact]
        public async Task UpdateTrackedBackends_BackendWithNoConfig_ShouldNotStartProber()
        {
            // Set up endpoints for probing, pretend that we have one replica.
            var endpointmanger = EndpointManagerGenerator(1);
            this.Mock<IEndpointManagerFactory>()
                .Setup(e => e.CreateEndpointManager())
                .Returns(endpointmanger);

            // Set up backends for probing, pretend that we have one services, each services have one replica.
            // Note we did not provide config for this service.
            this.backendManager.GetOrCreateItem("service0", item => { });

            // Start probing.
            var health = this.Create<HealthProbeWorker>();
            await health.UpdateTrackedBackends();

            // There is one service but it does not have config, no prober should be created and started.
            this.backendProber.Verify(p => p.Start(It.IsAny<AsyncSemaphore>()), Times.Never());
        }

        [Fact]
        public async Task UpdateTrackedBackends_BackendDidNotChange_StartsProberOnlyOnce()
        {
            // Set up endpoints for probing, pretend that we have three replica.
            var endpointmanger = EndpointManagerGenerator(1);
            this.Mock<IEndpointManagerFactory>()
                .Setup(e => e.CreateEndpointManager())
                .Returns(endpointmanger);

            // Set up backends for probing, pretend that we have one services, each services have one replica.
            this.backendManager.GetOrCreateItem("service0", item => { item.Config.Value = this.backendConfig; });

            var health = this.Create<HealthProbeWorker>();

            // Do probing double times
            await health.UpdateTrackedBackends();
            await health.UpdateTrackedBackends();

            // There is one service and service does not changed, prober should be only created and started once
            // no matter how many time probing is conducted.
            this.backendProber.Verify(p => p.Start(It.IsAny<AsyncSemaphore>()), Times.Once);
        }

        [Fact]
        public async Task UpdateTrackedBackends_BackendConfigChange_RecreatesProber()
        {
            // Set up endpoints for probing, pretend that we have three replica.
            var endpointmanger = EndpointManagerGenerator(3);
            this.Mock<IEndpointManagerFactory>()
                .Setup(e => e.CreateEndpointManager())
                .Returns(endpointmanger);

            // Set up backends for probing, pretend that we have one services, each services have three replica.
            this.backendManager.GetOrCreateItem("service0", item => { item.Config.Value = this.backendConfig; });

            var health = this.Create<HealthProbeWorker>();
            await health.UpdateTrackedBackends();

            // After the probing has already started, let's update the backend config for the service.
            this.backendManager.GetItems()[0].Config.Value = new BackendConfig(
                healthCheckOptions: new BackendConfig.BackendHealthCheckOptions(
                    enabled: true,
                    interval: TimeSpan.FromSeconds(1),
                    timeout: TimeSpan.FromSeconds(1),
                    port: 8000,
                    path: "/newexample"),
                loadBalancingOptions: default);
            await health.UpdateTrackedBackends();

            // After the config is updated, the program should discover this change, create a new prober,
            // stop and remove the previous prober. So two creation and one stop in total.
            this.backendProber.Verify(p => p.Start(It.IsAny<AsyncSemaphore>()), Times.Exactly(2));
            this.backendProber.Verify(p => p.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateTrackedBackends_BackendConfigDisabledProbing_StopsProber()
        {
            // Set up endpoints for probing, pretend that we have three replica.
            var endpointmanger = EndpointManagerGenerator(3);
            this.Mock<IEndpointManagerFactory>()
                .Setup(e => e.CreateEndpointManager())
                .Returns(endpointmanger);

            // Set up backends for probing, pretend that we have one services, each services have three replica.
            this.backendManager.GetOrCreateItem("service0", item => { item.Config.Value = this.backendConfig; });

            var health = this.Create<HealthProbeWorker>();
            await health.UpdateTrackedBackends();

            // After the probing has already started, let's update the backend config for the service.
            this.backendManager.GetItems()[0].Config.Value = new BackendConfig(
                healthCheckOptions: new BackendConfig.BackendHealthCheckOptions(
                    enabled: false,
                    interval: TimeSpan.FromSeconds(1),
                    timeout: TimeSpan.FromSeconds(1),
                    port: 8000,
                    path: "/newexample"),
                loadBalancingOptions: default);
            await health.UpdateTrackedBackends();

            // After the config is updated, the program should discover this change,
            // stop and remove the previous prober. So one creation and one stop in total.
            this.backendProber.Verify(p => p.Start(It.IsAny<AsyncSemaphore>()), Times.Once);
            this.backendProber.Verify(p => p.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateTrackedBackends_RemovedBackend_StopsProber()
        {
            // Set up endpoints for probing, pretend that we have three replica.
            var endpointmanger = EndpointManagerGenerator(3);
            this.Mock<IEndpointManagerFactory>()
                .Setup(e => e.CreateEndpointManager())
                .Returns(endpointmanger);

            // Set up backends for probing, pretend that we have one services, each services have three replica.
            this.backendManager.GetOrCreateItem("service0", item => { item.Config.Value = this.backendConfig; });

            // Start probing.
            var health = this.Create<HealthProbeWorker>();
            await health.UpdateTrackedBackends();

            // After the probing has already started, let's remove the backend.
            this.backendManager.TryRemoveItem("service0");
            await health.UpdateTrackedBackends();

            // After the backend is removed, the program should discover this removal,
            // stop and remove the prober for the removed service. So one creation and one stop in total.
            this.backendProber.Verify(p => p.Start(It.IsAny<AsyncSemaphore>()), Times.Once);
            this.backendProber.Verify(p => p.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task StopAsync_StopsAllProbers()
        {
            // Set up endpoints for probing, pretend that we have three replica.
            var endpointmanger = EndpointManagerGenerator(3);
            this.Mock<IEndpointManagerFactory>()
                .Setup(e => e.CreateEndpointManager())
                .Returns(endpointmanger);

            // Set up backends for probing, pretend that we have three services, each services have three replica.
            this.backendManager.GetOrCreateItem("service0", item => { item.Config.Value = this.backendConfig; });
            this.backendManager.GetOrCreateItem("service1", item => { item.Config.Value = this.backendConfig; });
            this.backendManager.GetOrCreateItem("service2", item => { item.Config.Value = this.backendConfig; });

            // Start probing.
            var health = this.Create<HealthProbeWorker>();
            await health.UpdateTrackedBackends();

            // Stop probing. We should expect three start and three stop.
            await health.StopAsync();
            this.backendProber.Verify(p => p.Start(It.IsAny<AsyncSemaphore>()), Times.Exactly(3));
            this.backendProber.Verify(p => p.StopAsync(), Times.Exactly(3));
        }

        private static EndpointManager EndpointManagerGenerator(int num)
        {
            var endpointmanger = new EndpointManager();
            for (int i = 0; i < num; i++)
            {
                endpointmanger.GetOrCreateItem("endpoint" + i.ToString(), item => { item.Config.Value = new EndpointConfig("url"); });
            }

            return endpointmanger;
        }
    }
}

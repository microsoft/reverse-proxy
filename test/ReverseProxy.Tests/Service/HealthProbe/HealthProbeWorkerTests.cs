// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.Time;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Microsoft.ReverseProxy.Utilities;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Service.HealthProbe
{
    public class HealthProbeWorkerTests : TestAutoMockBase
    {
        // The prober class is used by the healthProbe class, here we would mock the prober since we only care about the behavior of healthProbe class.
        // And we also want to customize the behavior of prober so we could have the full control of the unit test.
        private readonly Mock<IClusterProber> _clusterProber;
        private readonly IClusterManager _clusterManager;
        private readonly ClusterConfig _clusterConfig;

        public HealthProbeWorkerTests()
        {
            // Set up all the parameter needed for healthProberWorker.
            Provide<IMonotonicTimer, MonotonicTimer>();
            Provide<ILogger, Logger<HealthProbeWorker>>();
            Mock<IProxyHttpClientFactoryFactory>()
                .Setup(f => f.CreateFactory())
                .Returns(new Mock<IProxyHttpClientFactory>().Object);

            // Set up clusters. We are going to fake multiple service for us to probe.
            _clusterManager = Provide<IClusterManager, ClusterManager>();
            _clusterConfig = new ClusterConfig(
                healthCheckOptions: new ClusterConfig.ClusterHealthCheckOptions(
                    enabled: true,
                    interval: TimeSpan.FromSeconds(1),
                    timeout: TimeSpan.FromSeconds(1),
                    port: 8000,
                    path: "/example"),
                loadBalancingOptions: default,
                sessionAffinityOptions: default);

            // Set up prober. We do not want to let prober really perform any actions.
            // The behavior of prober should be tested in its own unit test, see "ClusterProberTests.cs".
            _clusterProber = new Mock<IClusterProber>();
            _clusterProber.Setup(p => p.Start(It.IsAny<SemaphoreSlim>()));
            _clusterProber.Setup(p => p.StopAsync());
            _clusterProber.Setup(p => p.ClusterId).Returns("service0");
            _clusterProber.Setup(p => p.Config).Returns(_clusterConfig);
            Mock<IClusterProberFactory>()
                .Setup(
                    r => r.CreateClusterProber(
                        It.IsAny<string>(),
                        It.IsAny<ClusterConfig>(),
                        It.IsAny<IDestinationManager>()))
                .Returns(_clusterProber.Object);
        }

        [Fact]
        public void Constructor_Works()
        {
            Create<HealthProbeWorker>();
        }

        [Fact]
        public async Task UpdateTrackedClusters_NoClusters_ShouldNotStartProber()
        {
            var health = Create<HealthProbeWorker>();
            await health.UpdateTrackedClusters();

            // There is no service, no prober should be created or started.
            _clusterProber.Verify(p => p.Start(It.IsAny<SemaphoreSlim>()), Times.Never());
        }

        [Fact]
        public async Task UpdateTrackedClusters_HasClusters_ShouldStartProber()
        {
            // Set up destinations for probing, pretend that we have three replica.
            var destinationmanger = DestinationManagerGenerator(3);
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(destinationmanger);

            // Set up clusters for probing, pretend that we have three services, each services have three replica.
            _clusterManager.GetOrCreateItem("service0", item => { item.Config.Value = _clusterConfig; });
            _clusterManager.GetOrCreateItem("service1", item => { item.Config.Value = _clusterConfig; });
            _clusterManager.GetOrCreateItem("service2", item => { item.Config.Value = _clusterConfig; });

            // Start probing.
            var health = Create<HealthProbeWorker>();
            await health.UpdateTrackedClusters();

            // There is three services, three prober should be created and started.
            _clusterProber.Verify(p => p.Start(It.IsAny<SemaphoreSlim>()), Times.Exactly(3));
        }

        [Fact]
        public async Task UpdateTrackedClusters_ProbeNotEnabled_ShouldNotStartProber()
        {
            // Set up destinations for probing, pretend that we have three replica.
            var destinationmanger = DestinationManagerGenerator(3);
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(destinationmanger);

            // Set up clusters for probing, pretend that we have three services, each services have three replica.
            _clusterManager.GetOrCreateItem(
                "service0",
                item =>
                {
                    item.Config.Value = new ClusterConfig(
                        healthCheckOptions: new ClusterConfig.ClusterHealthCheckOptions(
                            enabled: false,
                            interval: TimeSpan.FromSeconds(5),
                            timeout: TimeSpan.FromSeconds(20),
                            port: 1234,
                            path: "/"),
                        loadBalancingOptions: default,
                        sessionAffinityOptions: default);
                });

            // Start probing.
            var health = Create<HealthProbeWorker>();
            await health.UpdateTrackedClusters();

            // Probing is disabled for this cluster, no prober should be created or started.
            _clusterProber.Verify(p => p.Start(It.IsAny<SemaphoreSlim>()), Times.Never());
        }

        [Fact]
        public async Task UpdateTrackedClusters_ClusterWithNoConfig_ShouldNotStartProber()
        {
            // Set up destinations for probing, pretend that we have one replica.
            var destinationmanger = DestinationManagerGenerator(1);
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(destinationmanger);

            // Set up clusters for probing, pretend that we have one services, each services have one replica.
            // Note we did not provide config for this service.
            _clusterManager.GetOrCreateItem("service0", item => { });

            // Start probing.
            var health = Create<HealthProbeWorker>();
            await health.UpdateTrackedClusters();

            // There is one service but it does not have config, no prober should be created and started.
            _clusterProber.Verify(p => p.Start(It.IsAny<SemaphoreSlim>()), Times.Never());
        }

        [Fact]
        public async Task UpdateTrackedClusters_ClusterDidNotChange_StartsProberOnlyOnce()
        {
            // Set up destinations for probing, pretend that we have three replica.
            var destinationmanger = DestinationManagerGenerator(1);
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(destinationmanger);

            // Set up clusters for probing, pretend that we have one services, each services have one replica.
            _clusterManager.GetOrCreateItem("service0", item => { item.Config.Value = _clusterConfig; });

            var health = Create<HealthProbeWorker>();

            // Do probing double times
            await health.UpdateTrackedClusters();
            await health.UpdateTrackedClusters();

            // There is one service and service does not changed, prober should be only created and started once
            // no matter how many time probing is conducted.
            _clusterProber.Verify(p => p.Start(It.IsAny<SemaphoreSlim>()), Times.Once);
        }

        [Fact]
        public async Task UpdateTrackedClusters_ClusterConfigChange_RecreatesProber()
        {
            // Set up destinations for probing, pretend that we have three replica.
            var destinationmanger = DestinationManagerGenerator(3);
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(destinationmanger);

            // Set up clusters for probing, pretend that we have one services, each services have three replica.
            _clusterManager.GetOrCreateItem("service0", item => { item.Config.Value = _clusterConfig; });

            var health = Create<HealthProbeWorker>();
            await health.UpdateTrackedClusters();

            // After the probing has already started, let's update the cluster config for the service.
            _clusterManager.GetItems()[0].Config.Value = new ClusterConfig(
                healthCheckOptions: new ClusterConfig.ClusterHealthCheckOptions(
                    enabled: true,
                    interval: TimeSpan.FromSeconds(1),
                    timeout: TimeSpan.FromSeconds(1),
                    port: 8000,
                    path: "/newexample"),
                loadBalancingOptions: default,
                sessionAffinityOptions: default);
            await health.UpdateTrackedClusters();

            // After the config is updated, the program should discover this change, create a new prober,
            // stop and remove the previous prober. So two creation and one stop in total.
            _clusterProber.Verify(p => p.Start(It.IsAny<SemaphoreSlim>()), Times.Exactly(2));
            _clusterProber.Verify(p => p.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateTrackedClusters_ClusterConfigDisabledProbing_StopsProber()
        {
            // Set up destinations for probing, pretend that we have three replica.
            var destinationmanger = DestinationManagerGenerator(3);
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(destinationmanger);

            // Set up clusters for probing, pretend that we have one services, each services have three replica.
            _clusterManager.GetOrCreateItem("service0", item => { item.Config.Value = _clusterConfig; });

            var health = Create<HealthProbeWorker>();
            await health.UpdateTrackedClusters();

            // After the probing has already started, let's update the cluster config for the service.
            _clusterManager.GetItems()[0].Config.Value = new ClusterConfig(
                healthCheckOptions: new ClusterConfig.ClusterHealthCheckOptions(
                    enabled: false,
                    interval: TimeSpan.FromSeconds(1),
                    timeout: TimeSpan.FromSeconds(1),
                    port: 8000,
                    path: "/newexample"),
                loadBalancingOptions: default,
                sessionAffinityOptions: default);
            await health.UpdateTrackedClusters();

            // After the config is updated, the program should discover this change,
            // stop and remove the previous prober. So one creation and one stop in total.
            _clusterProber.Verify(p => p.Start(It.IsAny<SemaphoreSlim>()), Times.Once);
            _clusterProber.Verify(p => p.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateTrackedClusters_RemovedCluster_StopsProber()
        {
            // Set up destinations for probing, pretend that we have three replica.
            var destinationmanger = DestinationManagerGenerator(3);
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(destinationmanger);

            // Set up clusters for probing, pretend that we have one services, each services have three replica.
            _clusterManager.GetOrCreateItem("service0", item => { item.Config.Value = _clusterConfig; });

            // Start probing.
            var health = Create<HealthProbeWorker>();
            await health.UpdateTrackedClusters();

            // After the probing has already started, let's remove the cluster.
            _clusterManager.TryRemoveItem("service0");
            await health.UpdateTrackedClusters();

            // After the cluster is removed, the program should discover this removal,
            // stop and remove the prober for the removed service. So one creation and one stop in total.
            _clusterProber.Verify(p => p.Start(It.IsAny<SemaphoreSlim>()), Times.Once);
            _clusterProber.Verify(p => p.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task StopAsync_StopsAllProbers()
        {
            // Set up destinations for probing, pretend that we have three replica.
            var destinationmanger = DestinationManagerGenerator(3);
            Mock<IDestinationManagerFactory>()
                .Setup(e => e.CreateDestinationManager())
                .Returns(destinationmanger);

            // Set up clusters for probing, pretend that we have three services, each services have three replica.
            _clusterManager.GetOrCreateItem("service0", item => { item.Config.Value = _clusterConfig; });
            _clusterManager.GetOrCreateItem("service1", item => { item.Config.Value = _clusterConfig; });
            _clusterManager.GetOrCreateItem("service2", item => { item.Config.Value = _clusterConfig; });

            // Start probing.
            var health = Create<HealthProbeWorker>();
            await health.UpdateTrackedClusters();

            // Stop probing. We should expect three start and three stop.
            await health.StopAsync();
            _clusterProber.Verify(p => p.Start(It.IsAny<SemaphoreSlim>()), Times.Exactly(3));
            _clusterProber.Verify(p => p.StopAsync(), Times.Exactly(3));
        }

        private static DestinationManager DestinationManagerGenerator(int num)
        {
            var destinationmanger = new DestinationManager();
            for (var i = 0; i < num; i++)
            {
                destinationmanger.GetOrCreateItem("destination" + i.ToString(), item => { item.Config.Value = new DestinationConfig("url"); });
            }

            return destinationmanger;
        }
    }
}

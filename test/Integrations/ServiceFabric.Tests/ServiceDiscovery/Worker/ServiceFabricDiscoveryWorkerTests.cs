// <copyright file="ServiceFabricDiscoveryWorkerTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Health;
using System.Fabric.Query;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions;
using Moq;
using Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration.Tests
{
    public class ServiceFabricDiscoveryWorkerTests : TestAutoMockBase
    {
        /* TODO tests
            - Unhealthy replicas are not queried (not implemented yet)
            - Try different ListenerNames in the labels
            - Check that serviceFabricCaller failures don't complete crash
        */
        private static readonly Uri TestServiceName = new Uri("fabric:/App1/Svc1");

        private ServiceFabricServiceDiscoveryOptions scenarioOptions;

        // Scenario record
        private List<HealthReport> healthReports = new List<HealthReport>();
        private IList<ProxyRoute> routesRepo = new List<ProxyRoute>();
        private IDictionary<string, Cluster> clustersRepo = new Dictionary<string, Cluster>(StringComparer.Ordinal);

        public ServiceFabricDiscoveryWorkerTests(ITestOutputHelper testOutputHelper)
        {
            this.Provide<ILogger<ServiceFabricDiscoveryWorker>>(new XunitLogger<ServiceFabricDiscoveryWorker>(testOutputHelper));

            // Fake health report client
            this.Mock<IServiceFabricCaller>()
                .Setup(
                    m => m.ReportHealth(It.IsAny<HealthReport>(), It.IsAny<HealthReportSendOptions>())) // TODO: should also check for send options
                    .Callback((HealthReport report, HealthReportSendOptions sendOptions) => this.healthReports.Add(report));

            // Fake backends repo
            this.Mock<IClustersRepo>()
                .Setup(
                    m => m.SetClustersAsync(It.IsAny<IDictionary<string, Cluster>>(), It.IsAny<CancellationToken>()))
                .Callback((IDictionary<string, Cluster> clustersDict, CancellationToken token) => this.clustersRepo = clustersDict);

            // Fake routes repo
            this.Mock<IRoutesRepo>()
                .Setup(
                    m => m.SetRoutesAsync(It.IsAny<IList<ProxyRoute>>(), It.IsAny<CancellationToken>()))
                .Callback((IList<ProxyRoute> routesList, CancellationToken token) => this.routesRepo = routesList);
        }

        [Fact]
        public async void ExecuteAsync_NoAppsDiscovered_NoClusters()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            this.Mock_AppsResponse();

            // Act
            await this.RunScenarioAsync();

            // Assert
            this.clustersRepo.Should().BeEmpty();
            this.routesRepo.Should().BeEmpty();
            this.healthReports.Should().BeEmpty();
        }

        [Fact]
        public async void ExecuteAsync_NoExtensions_NoClusters()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            ApplicationWrapper application;
            this.Mock_AppsResponse(application = this.CreateApp_1Service_SingletonPartition_1Replica("MyCoolApp", "MyAwesomeService", out ServiceWrapper service, out ReplicaWrapper replica));
            this.Mock_ServiceLabels(application, service, new Dictionary<string, string>());

            // Act
            await this.RunScenarioAsync();

            // Assert
            this.clustersRepo.Should().BeEmpty();
            this.routesRepo.Should().BeEmpty();
            this.healthReports.Should().BeEmpty();
        }

        [Fact]
        public async void ExecuteAsync_GatewayNotEnabled_NoClusters()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            ApplicationWrapper application;
            this.Mock_AppsResponse(application = this.CreateApp_1Service_SingletonPartition_1Replica("MyCoolApp", "MyAwesomeService", out ServiceWrapper service, out ReplicaWrapper replica));
            this.Mock_ServiceLabels(application, service, new Dictionary<string, string>() { { "IslandGateway.Enable", "false" } });

            // Act
            await this.RunScenarioAsync();

            // Assert
            this.clustersRepo.Should().BeEmpty();
            this.routesRepo.Should().BeEmpty();
            this.healthReports.Should().BeEmpty();
        }

        [Fact]
        public async void ExecuteAsync_SingleServiceWithGatewayEnabled_OneClusterFound()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            ApplicationWrapper application, anotherApplication;
            this.Mock_AppsResponse(
                application = this.CreateApp_1StatelessService_2Partition_2ReplicasEach("MyApp", "MYService", out ServiceWrapper service, out List<ReplicaWrapper> replicas),
                anotherApplication = this.CreateApp_1StatelessService_2Partition_2ReplicasEach("AnotherApp", "AnotherService", out ServiceWrapper anotherService, out List<ReplicaWrapper> otherReplicas));
            this.Mock_ServiceLabels(application, service, labels);
            this.Mock_ServiceLabels(anotherApplication, anotherService, new Dictionary<string, string>());

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new Dictionary<string, Cluster>
            {
                {
                    TestClusterId,
                    ClusterWithDestinations(
                        LabelsParser.BuildCluster(TestServiceName, labels),
                        SFTestHelpers.BuildDestinationFromReplica(replicas[0]),
                        SFTestHelpers.BuildDestinationFromReplica(replicas[1]),
                        SFTestHelpers.BuildDestinationFromReplica(replicas[2]),
                        SFTestHelpers.BuildDestinationFromReplica(replicas[3]))
                },
            };
            var expectedRoutes = LabelsParser.BuildRoutes(TestServiceName, labels);

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.routesRepo.Should().BeEquivalentTo(expectedRoutes);
            this.AssertServiceHealthReported(service, HealthState.Ok);
            foreach (var replica in replicas)
            {
                this.AssertStatelessServiceInstanceHealthReported(replica, HealthState.Ok);
            }
            this.healthReports.Should().HaveCount(5);
        }

        [Fact]
        public async void ExecuteAsync_MultipleServicesWithGatewayEnabled_MultipleClustersFound()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterIdApp1Sv1 = "MyService123";
            const string TestClusterIdApp1Sv2 = "MyService234";
            const string TestClusterIdApp2Sv3 = "MyService345";
            var labels1 = SFTestHelpers.DummyLabels(TestClusterIdApp1Sv1);
            var labels2 = SFTestHelpers.DummyLabels(TestClusterIdApp1Sv2);
            var labels3 = SFTestHelpers.DummyLabels(TestClusterIdApp2Sv3);
            ApplicationWrapper application1, application2;
            this.Mock_AppsResponse(
                application1 = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MYService", out ServiceWrapper service1, out ReplicaWrapper replica1),
                application2 = this.CreateApp_2StatelessService_SingletonPartition_1Replica("MyApp2", "MyService2", "MyService3", out ServiceWrapper service2, out ServiceWrapper service3, out ReplicaWrapper replica2, out ReplicaWrapper replica3));

            this.Mock_ServiceLabels(application1, service1, labels1);
            this.Mock_ServiceLabels(application2, service2, labels2);
            this.Mock_ServiceLabels(application2, service3, labels3);

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new Dictionary<string, Cluster>
            {
                {
                    TestClusterIdApp1Sv1,
                    ClusterWithDestinations(
                        LabelsParser.BuildCluster(TestServiceName, labels1),
                        SFTestHelpers.BuildDestinationFromReplica(replica1))
                },
                {
                    TestClusterIdApp1Sv2,
                    ClusterWithDestinations(
                        LabelsParser.BuildCluster(TestServiceName, labels2),
                        SFTestHelpers.BuildDestinationFromReplica(replica2))
                },
                {
                    TestClusterIdApp2Sv3,
                    ClusterWithDestinations(
                        LabelsParser.BuildCluster(TestServiceName, labels3),
                        SFTestHelpers.BuildDestinationFromReplica(replica3))
                },
            };
            var expectedRoutes = new List<ProxyRoute>();
            expectedRoutes.AddRange(LabelsParser.BuildRoutes(TestServiceName, labels1));
            expectedRoutes.AddRange(LabelsParser.BuildRoutes(TestServiceName, labels2));
            expectedRoutes.AddRange(LabelsParser.BuildRoutes(TestServiceName, labels3));

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.routesRepo.Should().BeEquivalentTo(expectedRoutes);
            this.AssertServiceHealthReported(service1, HealthState.Ok);
            this.AssertServiceHealthReported(service2, HealthState.Ok);
            this.AssertServiceHealthReported(service3, HealthState.Ok);
            this.AssertStatelessServiceInstanceHealthReported(replica1, HealthState.Ok);
            this.AssertStatelessServiceInstanceHealthReported(replica2, HealthState.Ok);
            this.AssertStatelessServiceInstanceHealthReported(replica3, HealthState.Ok);
            this.healthReports.Should().HaveCount(6);
        }

        [Fact]
        public async void ExecuteAsync_OneServiceWithGatewayEnabledAndOneNotEnabled_OnlyTheOneEnabledFound()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterIdApp1Sv1 = "MyService123";
            const string TestClusterIdApp2Sv2 = "MyService234";
            var gatewayEnabledLabels = SFTestHelpers.DummyLabels(TestClusterIdApp1Sv1);
            var gatewayNotEnabledLabels = SFTestHelpers.DummyLabels(TestClusterIdApp2Sv2, false);
            ApplicationWrapper application1, application2;
            this.Mock_AppsResponse(
                application1 = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService1", out ServiceWrapper service1, out ReplicaWrapper replica1),
                application2 = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp2", "MyService2", out ServiceWrapper service2, out ReplicaWrapper replica2));

            this.Mock_ServiceLabels(application1, service1, gatewayEnabledLabels);
            this.Mock_ServiceLabels(application2, service2, gatewayNotEnabledLabels);

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new Dictionary<string, Cluster>
            {
                {
                    TestClusterIdApp1Sv1,
                    ClusterWithDestinations(
                        LabelsParser.BuildCluster(TestServiceName, gatewayEnabledLabels),
                        SFTestHelpers.BuildDestinationFromReplica(replica1))
                },
            };
            var expectedRoutes = new List<ProxyRoute>();
            expectedRoutes.AddRange(LabelsParser.BuildRoutes(TestServiceName, gatewayEnabledLabels));

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.routesRepo.Should().BeEquivalentTo(expectedRoutes);
            this.AssertServiceHealthReported(service1, HealthState.Ok);
            this.AssertStatelessServiceInstanceHealthReported(replica1, HealthState.Ok);
            this.healthReports.Should().HaveCount(2);
        }

        [Fact]
        public async void ExecuteAsync_GetLabelsFails_NoClustersAndBadHealthReported()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            ApplicationWrapper application;
            this.Mock_AppsResponse(
                application = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MYService", out ServiceWrapper service, out ReplicaWrapper replica1));

            this.Mock_ServiceLabelsException(application, service, new IslandGatewayConfigException("foo"));

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new List<Cluster>();
            var expectedRoutes = new List<ProxyRoute>();

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.routesRepo.Should().BeEquivalentTo(expectedRoutes);
            this.AssertServiceHealthReported(service, HealthState.Warning, (description) => description.Contains("foo"));
            this.healthReports.Should().HaveCount(1);
        }

        [Theory]
        [InlineData("IslandGateway.Backend.Partitioning.Count", "not a number")]
        [InlineData("IslandGateway.Backend.Healthcheck.Interval", "not an iso8601")]
        public async void ExecuteAsync_InvalidLabelsForCluster_NoClustersAndBadHealthReported(string keyToOverride, string value)
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            var labels = SFTestHelpers.DummyLabels("SomeClusterId");
            labels[keyToOverride] = value;
            ApplicationWrapper application;
            this.Mock_AppsResponse(
                application = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService", out ServiceWrapper service, out ReplicaWrapper replica));

            this.Mock_ServiceLabels(application, service, labels);

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new List<Cluster>();
            var expectedRoutes = new List<ProxyRoute>();

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.routesRepo.Should().BeEquivalentTo(expectedRoutes);
            this.AssertServiceHealthReported(service, HealthState.Warning, (description) =>
                description.Contains(keyToOverride)); // Check that the invalid key is mentioned in the description
            this.healthReports.Should().HaveCount(1);
        }

        [Theory]
        [InlineData("IslandGateway.Routes.MyRoute.Rule", null, true)] // Rule is mandatory
        [InlineData("IslandGateway.Routes.MyRoute.Priority", "not a number")]
        public async void ExecuteAsync_InvalidLabelsForRoutes_NoRoutesAndBadHealthReported(string keyToOverride, string value = null, bool remove = false)
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            var labels = new Dictionary<string, string>()
            {
                { "IslandGateway.Enable", "true" },
                { "IslandGateway.Backend.BackendId", "SomeClusterId" },
                { "IslandGateway.Routes.MyRoute.Rule", "Host('example.com)" },
                { "IslandGateway.Routes.MyRoute.Priority", "2" },
            };
            if (remove)
            {
                labels.Remove(keyToOverride);
            }
            else
            {
                labels[keyToOverride] = value;
            }
            ApplicationWrapper application;
            this.Mock_AppsResponse(
                application = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService", out ServiceWrapper service, out ReplicaWrapper replica));

            this.Mock_ServiceLabels(application, service, labels);

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new Dictionary<string, Cluster>
            {
                {
                    "SomeClusterId",
                    ClusterWithDestinations(
                        LabelsParser.BuildCluster(TestServiceName, labels),
                        SFTestHelpers.BuildDestinationFromReplica(replica))
                },
            };
            var expectedRoutes = new List<ProxyRoute>();

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.routesRepo.Should().BeEquivalentTo(expectedRoutes);
            this.AssertServiceHealthReported(service, HealthState.Warning, (description) =>
                description.Contains(keyToOverride)); // Check that the invalid key is mentioned in the description
            this.healthReports.Should().HaveCount(2);
        }

        [Fact]
        public async void ExecuteAsync_InvalidListenerNameForStatefulService_NoEndpointsAndBadHealthReported()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            labels["IslandGateway.Backend.ServiceFabric.ListenerName"] = "UnexistingListener";
            ApplicationWrapper application;
            this.Mock_AppsResponse(
                application = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService", out ServiceWrapper service, out ReplicaWrapper replica, serviceKind: ServiceKind.Stateful));

            this.Mock_ServiceLabels(application, service, labels);

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new Dictionary<string, Cluster>
            {
                { TestClusterId, LabelsParser.BuildCluster(TestServiceName, labels) },
            };
            var expectedRoutes = LabelsParser.BuildRoutes(TestServiceName, labels);

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.routesRepo.Should().BeEquivalentTo(expectedRoutes);
            this.AssertServiceHealthReported(service, HealthState.Ok);
            this.AssertStatefulServiceReplicaHealthReported(replica, HealthState.Warning, (description) =>
                description.StartsWith("Could not build endpoint for Island Gateway") &&
                description.Contains("UnexistingListener"));
            this.healthReports.Should().HaveCount(2);
        }

        [Fact]
        public async void ExecuteAsync_InvalidListenerNameForStatelessService_NoEndpointsAndBadHealthReported()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            labels["IslandGateway.Backend.ServiceFabric.ListenerName"] = "UnexistingListener";
            ApplicationWrapper application;
            this.Mock_AppsResponse(
                application = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService", out ServiceWrapper service, out ReplicaWrapper replica, serviceKind: ServiceKind.Stateless));

            this.Mock_ServiceLabels(application, service, labels);

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new Dictionary<string, Cluster>
            {
                { TestClusterId, LabelsParser.BuildCluster(TestServiceName, labels) },
            };
            var expectedRoutes = LabelsParser.BuildRoutes(TestServiceName, labels);

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.routesRepo.Should().BeEquivalentTo(expectedRoutes);
            this.AssertServiceHealthReported(service, HealthState.Ok);
            this.AssertStatelessServiceInstanceHealthReported(replica, HealthState.Warning, (description) =>
                description.StartsWith("Could not build endpoint for Island Gateway") &&
                description.Contains("UnexistingListener"));
            this.healthReports.Should().HaveCount(2);
        }

        [Fact]
        public async void ExecuteAsync_NotHttpsSchemeForStatelessService_NoEndpointsAndBadHealthReported()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            const string ServiceName = "fabric:/MyApp/MyService";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            labels["IslandGateway.Backend.ServiceFabric.ListenerName"] = "ExampleTeamEndpoint";
            ApplicationWrapper application;
            this.Mock_AppsResponse(
                application = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService", out ServiceWrapper service, out ReplicaWrapper replica, serviceKind: ServiceKind.Stateless));
            string nonHttpAddress = $"http://127.0.0.1/{ServiceName}/0";
            replica.ReplicaAddress = $"{{'Endpoints': {{'ExampleTeamEndpoint': '{nonHttpAddress}' }} }}".Replace("'", "\"");
            this.Mock_ServiceLabels(application, service, labels);

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new Dictionary<string, Cluster>
            {
                { TestClusterId, LabelsParser.BuildCluster(TestServiceName, labels) },
            };
            var expectedRoutes = LabelsParser.BuildRoutes(TestServiceName, labels);

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.routesRepo.Should().BeEquivalentTo(expectedRoutes);
            this.AssertServiceHealthReported(service, HealthState.Ok);
            this.AssertStatelessServiceInstanceHealthReported(replica, HealthState.Warning, (description) =>
                description.StartsWith("Could not build endpoint for Island Gateway") &&
                description.Contains("ExampleTeamEndpoint"));
            this.healthReports.Should().HaveCount(2);
        }

        [Fact]
        public async void ExecuteAsync_ValidListenerNameForStatelessService_Work()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            labels["IslandGateway.Backend.ServiceFabric.ListenerName"] = "ExampleTeamEndpoint";
            labels["IslandGateway.Backend.Healthcheck.ServiceFabric.ListenerName"] = "ExampleTeamHealthEndpoint";
            ApplicationWrapper application;
            this.Mock_AppsResponse(
                application = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService", out ServiceWrapper service, out ReplicaWrapper replica, serviceKind: ServiceKind.Stateless));
            replica.ReplicaAddress = this.MockReplicaAdressWithListenerName("MyApp", "MyService", new string[] { "ExampleTeamEndpoint", "ExampleTeamHealthEndpoint" });
            this.Mock_ServiceLabels(application, service, labels);

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new Dictionary<string, Cluster>
            {
                {
                    TestClusterId,
                    ClusterWithDestinations(
                        LabelsParser.BuildCluster(TestServiceName, labels),
                        SFTestHelpers.BuildDestinationFromReplica(replica, "ExampleTeamHealthEndpoint"))
                },
            };
            var expectedRoutes = LabelsParser.BuildRoutes(TestServiceName, labels);

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.routesRepo.Should().BeEquivalentTo(expectedRoutes);
            this.AssertServiceHealthReported(service, HealthState.Ok);
            this.AssertStatelessServiceInstanceHealthReported(replica, HealthState.Ok, (description) =>
                description.StartsWith("Successfully built"));
            this.healthReports.Should().HaveCount(2);
        }

        [Fact]
        public async void ExecuteAsync_SomeUnhealthyReplicas_OnlyHealthyReplicasAreUsed()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            ApplicationWrapper application;
            this.Mock_AppsResponse(
                application = this.CreateApp_1StatelessService_2Partition_2ReplicasEach(
                    "MyApp",
                    "MYService",
                    out ServiceWrapper service,
                    out List<ReplicaWrapper> replicas));
            this.Mock_ServiceLabels(application, service, labels);

            replicas[0].ReplicaStatus = ServiceReplicaStatus.Ready; // Should be used despite Warning health state
            replicas[0].HealthState = HealthState.Warning;

            replicas[1].ReplicaStatus = ServiceReplicaStatus.Ready; // Should be used
            replicas[1].HealthState = HealthState.Ok;

            replicas[2].ReplicaStatus = ServiceReplicaStatus.Ready; // Should be used despite Error health state
            replicas[2].HealthState = HealthState.Error;

            replicas[3].ReplicaStatus = ServiceReplicaStatus.Down; // Should be skipped because of status
            replicas[3].HealthState = HealthState.Ok;

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new Dictionary<string, Cluster>
            {
                {
                    TestClusterId,
                    ClusterWithDestinations(
                        LabelsParser.BuildCluster(TestServiceName, labels),
                        SFTestHelpers.BuildDestinationFromReplica(replicas[0]),
                        SFTestHelpers.BuildDestinationFromReplica(replicas[1]),
                        SFTestHelpers.BuildDestinationFromReplica(replicas[2]))
                },
            };
            var expectedRoutes = LabelsParser.BuildRoutes(TestServiceName, labels);

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.routesRepo.Should().BeEquivalentTo(expectedRoutes);
            this.AssertServiceHealthReported(service, HealthState.Ok);
            this.AssertStatelessServiceInstanceHealthReported(replicas[0], HealthState.Ok);
            this.AssertStatelessServiceInstanceHealthReported(replicas[1], HealthState.Ok);
            this.AssertStatelessServiceInstanceHealthReported(replicas[2], HealthState.Ok);
            this.healthReports.Should().HaveCount(4); // 1 service + 3 replicas = 4 health reports
        }

        [Fact]
        public async void ExecuteAsync_ReplicaHealthReportDisabled_ReplicasHealthIsNotReported()
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = false };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            ApplicationWrapper application;
            this.Mock_AppsResponse(
                application = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MYService", out ServiceWrapper service, out ReplicaWrapper replica));
            this.Mock_ServiceLabels(application, service, labels);

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new Dictionary<string, Cluster>
            {
                {
                    TestClusterId,
                    ClusterWithDestinations(
                        LabelsParser.BuildCluster(TestServiceName, labels),
                        SFTestHelpers.BuildDestinationFromReplica(replica))
                },
            };
            var expectedRoutes = LabelsParser.BuildRoutes(TestServiceName, labels);
            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.routesRepo.Should().BeEquivalentTo(expectedRoutes);
            this.AssertServiceHealthReported(service, HealthState.Ok);
            this.healthReports.Should().HaveCount(1);
        }

        [Theory]
        [InlineData("PrimaryOnly", ReplicaRole.Primary)]
        [InlineData("primaryonly", ReplicaRole.Primary)]
        [InlineData("SecondaryOnly", ReplicaRole.ActiveSecondary)]
        [InlineData("All", ReplicaRole.None)]
        [InlineData("All", ReplicaRole.Unknown)]
        [InlineData("All", ReplicaRole.Primary)]
        [InlineData("All", ReplicaRole.ActiveSecondary)]
        [InlineData("All", null)]
        public async void ExecuteAsync_StatefulService_SelectReplicaWork(string selectionMode, ReplicaRole? replicaRole)
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            labels["IslandGateway.Backend.ServiceFabric.StatefulReplicaSelectionMode"] = selectionMode;
            ApplicationWrapper application;
            this.Mock_AppsResponse(application = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MYService", out ServiceWrapper service, out ReplicaWrapper replica, serviceKind: ServiceKind.Stateful));
            this.Mock_ServiceLabels(application, service, labels);
            replica.ServiceKind = ServiceKind.Stateful;
            replica.Role = replicaRole;

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new Dictionary<string, Cluster>
            {
                {
                    TestClusterId,
                    ClusterWithDestinations(
                        LabelsParser.BuildCluster(TestServiceName, labels),
                        SFTestHelpers.BuildDestinationFromReplica(replica))
                },
            };

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.healthReports.Should().HaveCount(2);
        }

        [Theory]
        [InlineData("PrimaryOnly", ReplicaRole.None)]
        [InlineData("PrimaryOnly", ReplicaRole.Unknown)]
        [InlineData("PrimaryOnly", ReplicaRole.ActiveSecondary)]
        [InlineData("PrimaryOnly", null)]
        [InlineData("SecondaryOnly", ReplicaRole.None)]
        [InlineData("SecondaryOnly", ReplicaRole.Unknown)]
        [InlineData("SecondaryOnly", ReplicaRole.Primary)]
        [InlineData("SecondaryOnly", null)]
        public async void ExecuteAsync_StatefulService_SkipReplicaWork(string selectionMode, ReplicaRole? replicaRole)
        {
            // Setup
            this.scenarioOptions = new ServiceFabricServiceDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            labels["IslandGateway.Backend.ServiceFabric.StatefulReplicaSelectionMode"] = selectionMode;
            ApplicationWrapper application;
            this.Mock_AppsResponse(application = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MYService", out ServiceWrapper service, out ReplicaWrapper replica, serviceKind: ServiceKind.Stateful));
            this.Mock_ServiceLabels(application, service, labels);
            replica.ServiceKind = ServiceKind.Stateful;
            replica.Role = replicaRole;

            // Act
            await this.RunScenarioAsync();

            // Assert
            var expectedClusters = new Dictionary<string, Cluster>
            {
                { TestClusterId, LabelsParser.BuildCluster(TestServiceName, labels) },
            };

            this.clustersRepo.Should().BeEquivalentTo(expectedClusters);
            this.healthReports.Should().HaveCount(1);
        }

        private static Cluster ClusterWithDestinations(Cluster cluster, params KeyValuePair<string, Destination>[] destinations)
        {
            foreach (var destination in destinations)
            {
                cluster.Destinations.Add(destination.Key, destination.Value);
            }

            return cluster;
        }

        private async Task RunScenarioAsync()
        {
            if (this.scenarioOptions == null)
            {
                Assert.True(false, "The scenario options for the test are not set.");
            }
            var worker = this.Create<ServiceFabricDiscoveryWorker>();
            await worker.ExecuteAsync(this.scenarioOptions, CancellationToken.None);
        }

        // Assertion helpers
        private void AssertServiceHealthReported(ServiceWrapper service, HealthState expectedHealthState, Func<string, bool> descriptionCheck = null)
        {
            this.AssertHealthReported(
                expectedHealthState: expectedHealthState,
                descriptionCheck: descriptionCheck,
                extraChecks: report => (report as ServiceHealthReport) != null && (report as ServiceHealthReport).ServiceName == service.ServiceName,
                because: $"health '{expectedHealthState}' for service {service.ServiceName} should be reported");
        }
        private void AssertStatelessServiceInstanceHealthReported(ReplicaWrapper replica, HealthState expectedHealthState, Func<string, bool> descriptionCheck = null)
        {
            // TODO: test helpers don't return the fake partition ID so we can't verify replica.PartitioinId is the correct one. Pending to refactor the fixture helpers.
            this.AssertHealthReported(
                expectedHealthState: expectedHealthState,
                descriptionCheck: descriptionCheck,
                extraChecks: report => (report as StatelessServiceInstanceHealthReport) != null && (report as StatelessServiceInstanceHealthReport).InstanceId == replica.Id,
                because: $"health '{expectedHealthState}' for stateless instance {replica.Id} should be reported");
        }
        private void AssertStatefulServiceReplicaHealthReported(ReplicaWrapper replica, HealthState expectedHealthState, Func<string, bool> descriptionCheck = null)
        {
            // TODO: test helpers don't return the fake partition ID so we can't verify replica.PartitioinId is the correct one. Pending to refactor the fixture helpers.
            this.AssertHealthReported(
                expectedHealthState: expectedHealthState,
                descriptionCheck: descriptionCheck,
                extraChecks: report => (report as StatefulServiceReplicaHealthReport) != null && (report as StatefulServiceReplicaHealthReport).ReplicaId == replica.Id,
                because: $"health '{expectedHealthState}' for stateful replica {replica.Id} should be reported");
        }
        private void AssertHealthReported(
            HealthState expectedHealthState,
            Func<string, bool> descriptionCheck,
            Func<HealthReport, bool> extraChecks,
            string because)
        {
            TimeSpan expectedHealthReportTimeToLive = this.scenarioOptions.DiscoveryPeriod.Multiply(3);
            this.healthReports.Should().Contain(
                report =>
                    report.HealthInformation.SourceId == ServiceFabricDiscoveryWorker.HealthReportSourceId &&
                    report.HealthInformation.Property == ServiceFabricDiscoveryWorker.HealthReportProperty &&
                    report.HealthInformation.TimeToLive == expectedHealthReportTimeToLive &&
                    report.HealthInformation.HealthState == expectedHealthState &&
                    report.HealthInformation.RemoveWhenExpired == true &&
                    (extraChecks == null || extraChecks(report)) &&
                    (descriptionCheck == null || descriptionCheck(report.HealthInformation.Description)),
                because: because);
        }

        // Mocking helpers
        private ApplicationWrapper CreateApp_1Service_SingletonPartition_1Replica(
            string appTypeName,
            string serviceTypeName,
            out ServiceWrapper service,
            out ReplicaWrapper replica,
            ServiceKind serviceKind = ServiceKind.Stateless)
        {
            List<ReplicaWrapper> replicas;
            service = this.CreateService(appTypeName, serviceTypeName, 1, 1, out replicas, serviceKind);
            replica = replicas[0];
            this.Mock_ServicesResponse(new Uri($"fabric:/{appTypeName}"), service);
            return SFTestHelpers.FakeApp(appTypeName, appTypeName);
        }
        private ApplicationWrapper CreateApp_1StatelessService_2Partition_2ReplicasEach(
            string appTypeName,
            string serviceTypeName,
            out ServiceWrapper service,
            out List<ReplicaWrapper> replicas)
        {
            service = this.CreateService(appTypeName, serviceTypeName, 2, 2, out replicas);
            this.Mock_ServicesResponse(new Uri($"fabric:/{appTypeName}"), service);
            return SFTestHelpers.FakeApp(appTypeName, appTypeName);
        }
        private ApplicationWrapper CreateApp_2StatelessService_SingletonPartition_1Replica(
            string appTypeName,
            string serviceTypeName1,
            string serviceTypeName2,
            out ServiceWrapper service1,
            out ServiceWrapper service2,
            out ReplicaWrapper service1replica,
            out ReplicaWrapper service2replica)
        {
            List<ReplicaWrapper> replicas1, replicas2;
            service1 = this.CreateService(appTypeName, serviceTypeName1, 1, 1, out replicas1);
            service2 = this.CreateService(appTypeName, serviceTypeName2, 1, 1, out replicas2);
            service1replica = replicas1[0];
            service2replica = replicas2[0];
            this.Mock_ServicesResponse(new Uri($"fabric:/{appTypeName}"), service1, service2);
            return SFTestHelpers.FakeApp(appTypeName, appTypeName);
        }
        private ServiceWrapper CreateService(string appName, string serviceName, int numPartitions, int numReplicasPerPartition, out List<ReplicaWrapper> replicas, ServiceKind serviceKind = ServiceKind.Stateless)
        {
            var svcName = new Uri($"fabric:/{appName}/{serviceName}");
            var service = SFTestHelpers.FakeService(svcName, $"{appName}_{serviceName}_Type", serviceKind: serviceKind);
            replicas = new List<ReplicaWrapper>();

            var partitions = new List<Guid>();
            for (int i = 0; i < numPartitions; i++)
            {
                var partitionReplicas = Enumerable.Range(i * numReplicasPerPartition, numReplicasPerPartition).Select(replicaId => SFTestHelpers.FakeReplica(svcName, replicaId)).ToList();
                replicas.AddRange(partitionReplicas);
                var partition = SFTestHelpers.FakePartition();
                partitions.Add(partition);
                this.Mock_ReplicasResponse(partition, partitionReplicas.ToArray());
            }
            this.Mock_PartitionsResponse(svcName, partitions.ToArray());
            return service;
        }
        private void Mock_AppsResponse(params ApplicationWrapper[] apps)
        {
            this.Mock<IServiceFabricCaller>()
                .Setup(m => m.GetApplicationListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(apps.ToList());
        }
        private void Mock_ServicesResponse(Uri applicationName, params ServiceWrapper[] services)
        {
            this.Mock<IServiceFabricCaller>()
                .Setup(m => m.GetServiceListAsync(applicationName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(services.ToList());
        }
        private void Mock_PartitionsResponse(Uri serviceName, params Guid[] partitionIds)
        {
            this.Mock<IServiceFabricCaller>()
                .Setup(m => m.GetPartitionListAsync(serviceName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(partitionIds.ToList());
        }
        private void Mock_ReplicasResponse(Guid partitionId, params ReplicaWrapper[] replicas)
        {
            this.Mock<IServiceFabricCaller>()
                .Setup(m => m.GetReplicaListAsync(partitionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(replicas.ToList());
        }
        private void Mock_ServiceLabels(ApplicationWrapper application, ServiceWrapper service, Dictionary<string, string> labels)
        {
            this.Mock<IServiceFabricExtensionConfigProvider>()
                .Setup(m => m.GetExtensionLabelsAsync(application, service, It.IsAny<CancellationToken>()))
                .ReturnsAsync(labels);
        }
        private void Mock_ServiceLabelsException(ApplicationWrapper application, ServiceWrapper service, Exception ex)
        {
            this.Mock<IServiceFabricExtensionConfigProvider>()
                .Setup(m => m.GetExtensionLabelsAsync(application, service, It.IsAny<CancellationToken>()))
                .ThrowsAsync(ex);
        }

        private string MockReplicaAdressWithListenerName(string appName, string serviceName, string[] listenerNameList)
        {
            var serviceNameUri = new Uri($"fabric:/{appName}/{serviceName}");
            string address = $"https://127.0.0.1/{serviceNameUri.Authority}/0";

            var endpoints = new Dictionary<string, string>();
            foreach (var lisernerName in listenerNameList)
            {
                endpoints.Add(lisernerName, address);
            }

            string replicaAddress = JsonSerializer.Serialize(
                new
                {
                    Endpoints = endpoints,
                });

            return replicaAddress;
        }
    }
}

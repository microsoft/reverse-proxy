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
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.Service;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.ReverseProxy.ServiceFabric.Tests
{
    public class DiscovererTests : TestAutoMockBase
    {
        /* TODO tests
            - Unhealthy replicas are not queried (not implemented yet)
            - Try different ListenerNames in the labels
            - Check that serviceFabricCaller failures don't complete crash
        */
        private static readonly Uri _testServiceName = new Uri("fabric:/App1/Svc1");

        private readonly List<HealthReport> _healthReports = new List<HealthReport>();
        private ServiceFabricDiscoveryOptions _scenarioOptions;

        public DiscovererTests(ITestOutputHelper testOutputHelper)
        {
            Provide<ILogger<Discoverer>>(new XunitLogger<Discoverer>(testOutputHelper));

            // Fake health report client
            Mock<IServiceFabricCaller>()
                .Setup(
                    m => m.ReportHealth(It.IsAny<HealthReport>(), It.IsAny<HealthReportSendOptions>())) // TODO: should also check for send options
                    .Callback((HealthReport report, HealthReportSendOptions sendOptions) => _healthReports.Add(report));

            Mock<IOptionsMonitor<ServiceFabricDiscoveryOptions>>()
                .SetupGet(o => o.CurrentValue)
                .Returns(() => _scenarioOptions);

            Mock<IConfigValidator>()
                .Setup(v => v.ValidateClusterAsync(It.IsAny<Cluster>()))
                .ReturnsAsync(() => new List<Exception>());
            Mock<IConfigValidator>()
                .Setup(v => v.ValidateRouteAsync(It.IsAny<ProxyRoute>()))
                .ReturnsAsync(() => new List<Exception>());
        }

        [Fact]
        public async void ExecuteAsync_NoAppsDiscovered_NoClusters()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            Mock_AppsResponse();

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            routes.Should().BeEmpty();
            clusters.Should().BeEmpty();
            _healthReports.Should().BeEmpty();
        }

        [Fact]
        public async void ExecuteAsync_NoExtensions_NoClusters()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            ApplicationWrapper application;
            Mock_AppsResponse(application = CreateApp_1Service_SingletonPartition_1Replica("MyCoolApp", "MyAwesomeService", out var service, out _));
            Mock_ServiceLabels(application, service, new Dictionary<string, string>());

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            routes.Should().BeEmpty();
            clusters.Should().BeEmpty();
            _healthReports.Should().BeEmpty();
        }

        [Fact]
        public async void ExecuteAsync_GatewayNotEnabled_NoClusters()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            ApplicationWrapper application;
            Mock_AppsResponse(application = CreateApp_1Service_SingletonPartition_1Replica("MyCoolApp", "MyAwesomeService", out var service, out _));
            Mock_ServiceLabels(application, service, new Dictionary<string, string>() { { "YARP.Enable", "false" } });

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            routes.Should().BeEmpty();
            clusters.Should().BeEmpty();
            _healthReports.Should().BeEmpty();
        }

        [Fact]
        public async void ExecuteAsync_SingleServiceWithGatewayEnabled_OneClusterFound()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            ApplicationWrapper application, anotherApplication;
            Mock_AppsResponse(
                application = CreateApp_1StatelessService_2Partition_2ReplicasEach("MyApp", "MYService", out var service, out var replicas),
                anotherApplication = CreateApp_1StatelessService_2Partition_2ReplicasEach("AnotherApp", "AnotherService", out var anotherService, out var otherReplicas));
            Mock_ServiceLabels(application, service, labels);
            Mock_ServiceLabels(anotherApplication, anotherService, new Dictionary<string, string>());

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            var expectedClusters = new[]
            {
                ClusterWithDestinations(
                    LabelsParser.BuildCluster(_testServiceName, labels),
                    SFTestHelpers.BuildDestinationFromReplica(replicas[0]),
                    SFTestHelpers.BuildDestinationFromReplica(replicas[1]),
                    SFTestHelpers.BuildDestinationFromReplica(replicas[2]),
                    SFTestHelpers.BuildDestinationFromReplica(replicas[3])),
            };
            var expectedRoutes = LabelsParser.BuildRoutes(_testServiceName, labels);

            routes.Should().BeEquivalentTo(expectedRoutes);
            clusters.Should().BeEquivalentTo(expectedClusters);
            AssertServiceHealthReported(service, HealthState.Ok);
            foreach (var replica in replicas)
            {
                AssertStatelessServiceInstanceHealthReported(replica, HealthState.Ok);
            }
            _healthReports.Should().HaveCount(5);
        }

        [Fact]
        public async void ExecuteAsync_MultipleServicesWithGatewayEnabled_MultipleClustersFound()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterIdApp1Sv1 = "MyService123";
            const string TestClusterIdApp1Sv2 = "MyService234";
            const string TestClusterIdApp2Sv3 = "MyService345";
            var labels1 = SFTestHelpers.DummyLabels(TestClusterIdApp1Sv1);
            var labels2 = SFTestHelpers.DummyLabels(TestClusterIdApp1Sv2);
            var labels3 = SFTestHelpers.DummyLabels(TestClusterIdApp2Sv3);
            ApplicationWrapper application1, application2;
            Mock_AppsResponse(
                application1 = CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MYService", out var service1, out var replica1),
                application2 = CreateApp_2StatelessService_SingletonPartition_1Replica("MyApp2", "MyService2", "MyService3", out var service2, out var service3, out var replica2, out var replica3));

            Mock_ServiceLabels(application1, service1, labels1);
            Mock_ServiceLabels(application2, service2, labels2);
            Mock_ServiceLabels(application2, service3, labels3);

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            var expectedClusters = new[]
            {
                ClusterWithDestinations(
                    LabelsParser.BuildCluster(_testServiceName, labels1),
                    SFTestHelpers.BuildDestinationFromReplica(replica1)),
                ClusterWithDestinations(
                    LabelsParser.BuildCluster(_testServiceName, labels2),
                    SFTestHelpers.BuildDestinationFromReplica(replica2)),
                ClusterWithDestinations(
                    LabelsParser.BuildCluster(_testServiceName, labels3),
                    SFTestHelpers.BuildDestinationFromReplica(replica3)),
            };
            var expectedRoutes = new List<ProxyRoute>();
            expectedRoutes.AddRange(LabelsParser.BuildRoutes(_testServiceName, labels1));
            expectedRoutes.AddRange(LabelsParser.BuildRoutes(_testServiceName, labels2));
            expectedRoutes.AddRange(LabelsParser.BuildRoutes(_testServiceName, labels3));

            clusters.Should().BeEquivalentTo(expectedClusters);
            routes.Should().BeEquivalentTo(expectedRoutes);
            AssertServiceHealthReported(service1, HealthState.Ok);
            AssertServiceHealthReported(service2, HealthState.Ok);
            AssertServiceHealthReported(service3, HealthState.Ok);
            AssertStatelessServiceInstanceHealthReported(replica1, HealthState.Ok);
            AssertStatelessServiceInstanceHealthReported(replica2, HealthState.Ok);
            AssertStatelessServiceInstanceHealthReported(replica3, HealthState.Ok);
            _healthReports.Should().HaveCount(6);
        }

        [Fact]
        public async void ExecuteAsync_OneServiceWithGatewayEnabledAndOneNotEnabled_OnlyTheOneEnabledFound()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterIdApp1Sv1 = "MyService123";
            const string TestClusterIdApp2Sv2 = "MyService234";
            var gatewayEnabledLabels = SFTestHelpers.DummyLabels(TestClusterIdApp1Sv1);
            var gatewayNotEnabledLabels = SFTestHelpers.DummyLabels(TestClusterIdApp2Sv2, false);
            ApplicationWrapper application1, application2;
            Mock_AppsResponse(
                application1 = CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService1", out var service1, out var replica1),
                application2 = CreateApp_1Service_SingletonPartition_1Replica("MyApp2", "MyService2", out var service2, out var replica2));

            Mock_ServiceLabels(application1, service1, gatewayEnabledLabels);
            Mock_ServiceLabels(application2, service2, gatewayNotEnabledLabels);

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            var expectedClusters = new[]
            {
                ClusterWithDestinations(
                    LabelsParser.BuildCluster(_testServiceName, gatewayEnabledLabels),
                    SFTestHelpers.BuildDestinationFromReplica(replica1)),
            };
            var expectedRoutes = new List<ProxyRoute>();
            expectedRoutes.AddRange(LabelsParser.BuildRoutes(_testServiceName, gatewayEnabledLabels));

            clusters.Should().BeEquivalentTo(expectedClusters);
            routes.Should().BeEquivalentTo(expectedRoutes);
            AssertServiceHealthReported(service1, HealthState.Ok);
            AssertStatelessServiceInstanceHealthReported(replica1, HealthState.Ok);
            _healthReports.Should().HaveCount(2);
        }

        [Fact]
        public async void ExecuteAsync_GetLabelsFails_NoClustersAndBadHealthReported()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            ApplicationWrapper application;
            Mock_AppsResponse(
                application = CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MYService", out var service, out var replica1));

            Mock_ServiceLabelsException(application, service, new ConfigException("foo"));

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            clusters.Should().BeEmpty();
            routes.Should().BeEmpty();
            AssertServiceHealthReported(service, HealthState.Warning, (description) => description.Contains("foo"));
            _healthReports.Should().HaveCount(1);
        }

        [Theory]
        [InlineData("YARP.Backend.Healthcheck.Active.Interval", "not a number")]
        public async void ExecuteAsync_InvalidLabelsForCluster_NoClustersAndBadHealthReported(string keyToOverride, string value)
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            var labels = SFTestHelpers.DummyLabels("SomeClusterId");
            labels[keyToOverride] = value;
            ApplicationWrapper application;
            Mock_AppsResponse(
                application = CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService", out var service, out var replica));

            Mock_ServiceLabels(application, service, labels);

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            clusters.Should().BeEmpty();
            routes.Should().BeEmpty();
            AssertServiceHealthReported(service, HealthState.Warning, (description) =>
                description.Contains(keyToOverride)); // Check that the invalid key is mentioned in the description
            _healthReports.Should().HaveCount(1);
        }

        [Fact]
        public async void ExecuteAsync_InvalidRouteOrder_NoRoutesAndBadHealthReported()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            var labels = new Dictionary<string, string>()
            {
                { "YARP.Enable", "true" },
                { "YARP.Backend.BackendId", "SomeClusterId" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Order", "not a number" },
            };
            ApplicationWrapper application;
            Mock_AppsResponse(
                application = CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService", out var service, out var replica));

            Mock_ServiceLabels(application, service, labels);

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            var expectedClusters = new[]
            {
                ClusterWithDestinations(
                    LabelsParser.BuildCluster(_testServiceName, labels),
                    SFTestHelpers.BuildDestinationFromReplica(replica)),
            };
            var expectedRoutes = new List<ProxyRoute>();

            clusters.Should().BeEquivalentTo(expectedClusters);
            routes.Should().BeEmpty();
            AssertServiceHealthReported(service, HealthState.Warning, (description) =>
                description.Contains("Order")); // Check that the invalid key is mentioned in the description
            _healthReports.Should().HaveCount(2);
        }

        [Fact]
        public async void ExecuteAsync_InvalidListenerNameForStatefulService_NoEndpointsAndBadHealthReported()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            labels["YARP.Backend.ServiceFabric.ListenerName"] = "UnexistingListener";
            ApplicationWrapper application;
            Mock_AppsResponse(
                application = CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService", out var service, out var replica, serviceKind: ServiceKind.Stateful));

            Mock_ServiceLabels(application, service, labels);

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            var expectedClusters = new[]
            {
                LabelsParser.BuildCluster(_testServiceName, labels),
            };
            var expectedRoutes = LabelsParser.BuildRoutes(_testServiceName, labels);

            clusters.Should().BeEquivalentTo(expectedClusters);
            routes.Should().BeEquivalentTo(expectedRoutes);
            AssertServiceHealthReported(service, HealthState.Ok);
            AssertStatefulServiceReplicaHealthReported(replica, HealthState.Warning, (description) =>
                description.StartsWith("Could not build endpoint for Island Gateway") &&
                description.Contains("UnexistingListener"));
            _healthReports.Should().HaveCount(2);
        }

        [Fact]
        public async void ExecuteAsync_InvalidListenerNameForStatelessService_NoEndpointsAndBadHealthReported()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            labels["YARP.Backend.ServiceFabric.ListenerName"] = "UnexistingListener";
            ApplicationWrapper application;
            Mock_AppsResponse(
                application = CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService", out var service, out var replica, serviceKind: ServiceKind.Stateless));

            Mock_ServiceLabels(application, service, labels);

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            var expectedClusters = new[]
            {
                LabelsParser.BuildCluster(_testServiceName, labels),
            };
            var expectedRoutes = LabelsParser.BuildRoutes(_testServiceName, labels);

            clusters.Should().BeEquivalentTo(expectedClusters);
            routes.Should().BeEquivalentTo(expectedRoutes);
            AssertServiceHealthReported(service, HealthState.Ok);
            AssertStatelessServiceInstanceHealthReported(replica, HealthState.Warning, (description) =>
                description.StartsWith("Could not build endpoint for Island Gateway") &&
                description.Contains("UnexistingListener"));
            _healthReports.Should().HaveCount(2);
        }

        [Fact]
        public async void ExecuteAsync_NotHttpsSchemeForStatelessService_NoEndpointsAndBadHealthReported()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            const string ServiceName = "fabric:/MyApp/MyService";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            labels["YARP.Backend.ServiceFabric.ListenerName"] = "ExampleTeamEndpoint";
            ApplicationWrapper application;
            Mock_AppsResponse(
                application = CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService", out var service, out var replica, serviceKind: ServiceKind.Stateless));
            var nonHttpAddress = $"http://127.0.0.1/{ServiceName}/0";
            replica.ReplicaAddress = $"{{'Endpoints': {{'ExampleTeamEndpoint': '{nonHttpAddress}' }} }}".Replace("'", "\"");
            Mock_ServiceLabels(application, service, labels);

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            var expectedClusters = new[]
            {
                LabelsParser.BuildCluster(_testServiceName, labels),
            };
            var expectedRoutes = LabelsParser.BuildRoutes(_testServiceName, labels);

            clusters.Should().BeEquivalentTo(expectedClusters);
            routes.Should().BeEquivalentTo(expectedRoutes);
            AssertServiceHealthReported(service, HealthState.Ok);
            AssertStatelessServiceInstanceHealthReported(replica, HealthState.Warning, (description) =>
                description.StartsWith("Could not build endpoint for Island Gateway") &&
                description.Contains("ExampleTeamEndpoint"));
            _healthReports.Should().HaveCount(2);
        }

        [Fact]
        public async void ExecuteAsync_ValidListenerNameForStatelessService_Work()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            labels["YARP.Backend.ServiceFabric.ListenerName"] = "ExampleTeamEndpoint";
            labels["YARP.Backend.Healthcheck.Active.ServiceFabric.ListenerName"] = "ExampleTeamHealthEndpoint";
            ApplicationWrapper application;
            Mock_AppsResponse(
                application = CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MyService", out var service, out var replica, serviceKind: ServiceKind.Stateless));
            replica.ReplicaAddress = MockReplicaAdressWithListenerName("MyApp", "MyService", new string[] { "ExampleTeamEndpoint", "ExampleTeamHealthEndpoint" });
            Mock_ServiceLabels(application, service, labels);

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            var expectedClusters = new[]
            {
                ClusterWithDestinations(
                    LabelsParser.BuildCluster(_testServiceName, labels),
                    SFTestHelpers.BuildDestinationFromReplica(replica, "ExampleTeamHealthEndpoint")),
            };
            var expectedRoutes = LabelsParser.BuildRoutes(_testServiceName, labels);

            clusters.Should().BeEquivalentTo(expectedClusters);
            routes.Should().BeEquivalentTo(expectedRoutes);
            AssertServiceHealthReported(service, HealthState.Ok);
            AssertStatelessServiceInstanceHealthReported(replica, HealthState.Ok, (description) =>
                description.StartsWith("Successfully built"));
            _healthReports.Should().HaveCount(2);
        }

        [Fact]
        public async void ExecuteAsync_SomeUnhealthyReplicas_OnlyHealthyReplicasAreUsed()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            ApplicationWrapper application;
            Mock_AppsResponse(
                application = CreateApp_1StatelessService_2Partition_2ReplicasEach(
                    "MyApp",
                    "MYService",
                    out var service,
                    out var replicas));
            Mock_ServiceLabels(application, service, labels);

            replicas[0].ReplicaStatus = ServiceReplicaStatus.Ready; // Should be used despite Warning health state
            replicas[0].HealthState = HealthState.Warning;

            replicas[1].ReplicaStatus = ServiceReplicaStatus.Ready; // Should be used
            replicas[1].HealthState = HealthState.Ok;

            replicas[2].ReplicaStatus = ServiceReplicaStatus.Ready; // Should be used despite Error health state
            replicas[2].HealthState = HealthState.Error;

            replicas[3].ReplicaStatus = ServiceReplicaStatus.Down; // Should be skipped because of status
            replicas[3].HealthState = HealthState.Ok;

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            var expectedClusters = new[]
            {
                ClusterWithDestinations(
                    LabelsParser.BuildCluster(_testServiceName, labels),
                    SFTestHelpers.BuildDestinationFromReplica(replicas[0]),
                    SFTestHelpers.BuildDestinationFromReplica(replicas[1]),
                    SFTestHelpers.BuildDestinationFromReplica(replicas[2])),
            };
            var expectedRoutes = LabelsParser.BuildRoutes(_testServiceName, labels);

            clusters.Should().BeEquivalentTo(expectedClusters);
            routes.Should().BeEquivalentTo(expectedRoutes);
            AssertServiceHealthReported(service, HealthState.Ok);
            AssertStatelessServiceInstanceHealthReported(replicas[0], HealthState.Ok);
            AssertStatelessServiceInstanceHealthReported(replicas[1], HealthState.Ok);
            AssertStatelessServiceInstanceHealthReported(replicas[2], HealthState.Ok);
            _healthReports.Should().HaveCount(4); // 1 service + 3 replicas = 4 health reports
        }

        [Fact]
        public async void ExecuteAsync_ReplicaHealthReportDisabled_ReplicasHealthIsNotReported()
        {
            // Setup
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = false };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            ApplicationWrapper application;
            Mock_AppsResponse(
                application = CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MYService", out var service, out var replica));
            Mock_ServiceLabels(application, service, labels);

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            var expectedClusters = new[]
            {
                ClusterWithDestinations(
                    LabelsParser.BuildCluster(_testServiceName, labels),
                    SFTestHelpers.BuildDestinationFromReplica(replica)),
            };
            var expectedRoutes = LabelsParser.BuildRoutes(_testServiceName, labels);
            clusters.Should().BeEquivalentTo(expectedClusters);
            routes.Should().BeEquivalentTo(expectedRoutes);
            AssertServiceHealthReported(service, HealthState.Ok);
            _healthReports.Should().HaveCount(1);
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
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            labels["YARP.Backend.ServiceFabric.StatefulReplicaSelectionMode"] = selectionMode;
            ApplicationWrapper application;
            Mock_AppsResponse(application = CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MYService", out var service, out var replica, serviceKind: ServiceKind.Stateful));
            Mock_ServiceLabels(application, service, labels);
            replica.ServiceKind = ServiceKind.Stateful;
            replica.Role = replicaRole;

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            var expectedClusters = new[]
            {
                ClusterWithDestinations(
                    LabelsParser.BuildCluster(_testServiceName, labels),
                    SFTestHelpers.BuildDestinationFromReplica(replica)),
            };

            clusters.Should().BeEquivalentTo(expectedClusters);
            _healthReports.Should().HaveCount(2);
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
            _scenarioOptions = new ServiceFabricDiscoveryOptions { ReportReplicasHealth = true };
            const string TestClusterId = "MyService123";
            var labels = SFTestHelpers.DummyLabels(TestClusterId);
            labels["YARP.Backend.ServiceFabric.StatefulReplicaSelectionMode"] = selectionMode;
            ApplicationWrapper application;
            Mock_AppsResponse(application = CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MYService", out var service, out var replica, serviceKind: ServiceKind.Stateful));
            Mock_ServiceLabels(application, service, labels);
            replica.ServiceKind = ServiceKind.Stateful;
            replica.Role = replicaRole;

            // Act
            var (routes, clusters) = await RunScenarioAsync();

            // Assert
            var expectedClusters = new[]
            {
                LabelsParser.BuildCluster(_testServiceName, labels),
            };

            clusters.Should().BeEquivalentTo(expectedClusters);
            _healthReports.Should().HaveCount(1);
        }

        private static Cluster ClusterWithDestinations(Cluster cluster, params KeyValuePair<string, Destination>[] destinations)
        {
            foreach (var destination in destinations)
            {
                cluster.Destinations.Add(destination.Key, destination.Value);
            }

            return cluster;
        }

        private async Task<(IReadOnlyList<ProxyRoute> Routes, IReadOnlyList<Cluster> Clusters)> RunScenarioAsync()
        {
            if (_scenarioOptions == null)
            {
                Assert.True(false, "The scenario options for the test are not set.");
            }
            var worker = Create<Discoverer>();
            return await worker.DiscoverAsync(CancellationToken.None);
        }

        // Assertion helpers
        private void AssertServiceHealthReported(ServiceWrapper service, HealthState expectedHealthState, Func<string, bool> descriptionCheck = null)
        {
            AssertHealthReported(
                expectedHealthState: expectedHealthState,
                descriptionCheck: descriptionCheck,
                extraChecks: report => (report as ServiceHealthReport) != null && (report as ServiceHealthReport).ServiceName == service.ServiceName,
                because: $"health '{expectedHealthState}' for service {service.ServiceName} should be reported");
        }
        private void AssertStatelessServiceInstanceHealthReported(ReplicaWrapper replica, HealthState expectedHealthState, Func<string, bool> descriptionCheck = null)
        {
            // TODO: test helpers don't return the fake partition ID so we can't verify replica.PartitioinId is the correct one. Pending to refactor the fixture helpers.
            AssertHealthReported(
                expectedHealthState: expectedHealthState,
                descriptionCheck: descriptionCheck,
                extraChecks: report => (report as StatelessServiceInstanceHealthReport) != null && (report as StatelessServiceInstanceHealthReport).InstanceId == replica.Id,
                because: $"health '{expectedHealthState}' for stateless instance {replica.Id} should be reported");
        }
        private void AssertStatefulServiceReplicaHealthReported(ReplicaWrapper replica, HealthState expectedHealthState, Func<string, bool> descriptionCheck = null)
        {
            // TODO: test helpers don't return the fake partition ID so we can't verify replica.PartitioinId is the correct one. Pending to refactor the fixture helpers.
            AssertHealthReported(
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
            var expectedHealthReportTimeToLive = _scenarioOptions.DiscoveryPeriod.Multiply(3);
            _healthReports.Should().Contain(
                report =>
                    report.HealthInformation.SourceId == Discoverer.HealthReportSourceId &&
                    report.HealthInformation.Property == Discoverer.HealthReportProperty &&
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
            service = CreateService(appTypeName, serviceTypeName, 1, 1, out var replicas, serviceKind);
            replica = replicas[0];
            Mock_ServicesResponse(new Uri($"fabric:/{appTypeName}"), service);
            return SFTestHelpers.FakeApp(appTypeName, appTypeName);
        }
        private ApplicationWrapper CreateApp_1StatelessService_2Partition_2ReplicasEach(
            string appTypeName,
            string serviceTypeName,
            out ServiceWrapper service,
            out List<ReplicaWrapper> replicas)
        {
            service = CreateService(appTypeName, serviceTypeName, 2, 2, out replicas);
            Mock_ServicesResponse(new Uri($"fabric:/{appTypeName}"), service);
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
            service1 = CreateService(appTypeName, serviceTypeName1, 1, 1, out var replicas1);
            service2 = CreateService(appTypeName, serviceTypeName2, 1, 1, out var replicas2);
            service1replica = replicas1[0];
            service2replica = replicas2[0];
            Mock_ServicesResponse(new Uri($"fabric:/{appTypeName}"), service1, service2);
            return SFTestHelpers.FakeApp(appTypeName, appTypeName);
        }
        private ServiceWrapper CreateService(string appName, string serviceName, int numPartitions, int numReplicasPerPartition, out List<ReplicaWrapper> replicas, ServiceKind serviceKind = ServiceKind.Stateless)
        {
            var svcName = new Uri($"fabric:/{appName}/{serviceName}");
            var service = SFTestHelpers.FakeService(svcName, $"{appName}_{serviceName}_Type", serviceKind: serviceKind);
            replicas = new List<ReplicaWrapper>();

            var partitions = new List<Guid>();
            for (var i = 0; i < numPartitions; i++)
            {
                var partitionReplicas = Enumerable.Range(i * numReplicasPerPartition, numReplicasPerPartition).Select(replicaId => SFTestHelpers.FakeReplica(svcName, replicaId)).ToList();
                replicas.AddRange(partitionReplicas);
                var partition = SFTestHelpers.FakePartition();
                partitions.Add(partition);
                Mock_ReplicasResponse(partition, partitionReplicas.ToArray());
            }
            Mock_PartitionsResponse(svcName, partitions.ToArray());
            return service;
        }
        private void Mock_AppsResponse(params ApplicationWrapper[] apps)
        {
            Mock<IServiceFabricCaller>()
                .Setup(m => m.GetApplicationListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(apps.ToList());
        }
        private void Mock_ServicesResponse(Uri applicationName, params ServiceWrapper[] services)
        {
            Mock<IServiceFabricCaller>()
                .Setup(m => m.GetServiceListAsync(applicationName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(services.ToList());
        }
        private void Mock_PartitionsResponse(Uri serviceName, params Guid[] partitionIds)
        {
            Mock<IServiceFabricCaller>()
                .Setup(m => m.GetPartitionListAsync(serviceName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(partitionIds.ToList());
        }
        private void Mock_ReplicasResponse(Guid partitionId, params ReplicaWrapper[] replicas)
        {
            Mock<IServiceFabricCaller>()
                .Setup(m => m.GetReplicaListAsync(partitionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(replicas.ToList());
        }
        private void Mock_ServiceLabels(ApplicationWrapper application, ServiceWrapper service, Dictionary<string, string> labels)
        {
            Mock<IServiceExtensionLabelsProvider>()
                .Setup(m => m.GetExtensionLabelsAsync(application, service, It.IsAny<CancellationToken>()))
                .ReturnsAsync(labels);
        }
        private void Mock_ServiceLabelsException(ApplicationWrapper application, ServiceWrapper service, Exception ex)
        {
            Mock<IServiceExtensionLabelsProvider>()
                .Setup(m => m.GetExtensionLabelsAsync(application, service, It.IsAny<CancellationToken>()))
                .ThrowsAsync(ex);
        }

        private string MockReplicaAdressWithListenerName(string appName, string serviceName, string[] listenerNameList)
        {
            var serviceNameUri = new Uri($"fabric:/{appName}/{serviceName}");
            var address = $"https://127.0.0.1/{serviceNameUri.Authority}/0";

            var endpoints = new Dictionary<string, string>();
            foreach (var lisernerName in listenerNameList)
            {
                endpoints.Add(lisernerName, address);
            }

            var replicaAddress = JsonSerializer.Serialize(
                new {
                    Endpoints = endpoints,
                });

            return replicaAddress;
        }
    }
}

// <copyright file="ServiceFabricDiscoveryWorkerTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using FluentAssertions;
using IslandGateway.Common.Abstractions.Telemetry;
using IslandGateway.Common.Telemetry;
using IslandGateway.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace IslandGateway.ServiceFabricIntegration.Tests
{
    public class ServiceFabricDiscoveryWorkerTests : TestAutoMockBase
    {
        /* TODO tests
            - Try different ListenerNames in the labels
            - Try different failures and check those don't cause a cumplete crash
        */

        // Scenario record
        private IList<Backend> backendsRepo = new List<Backend>();
        private Dictionary<string, IList<BackendEndpoint>> endpointsRepo = new Dictionary<string, IList<BackendEndpoint>>();

        public ServiceFabricDiscoveryWorkerTests(ITestOutputHelper testOutputHelper)
        {
            this.Provide<IOperationLogger, TextOperationLogger>();
            this.Provide<ILogger<ServiceFabricDiscoveryWorker>>(new XunitLogger<ServiceFabricDiscoveryWorker>(testOutputHelper));

            // Fake backends repo
            this.Mock<IBackendsRepo>()
                .Setup(
                    m => m.SetBackendsAsync(It.IsAny<IList<Backend>>(), It.IsAny<CancellationToken>()))
                .Callback((IList<Backend> backendsList, CancellationToken token) => this.backendsRepo = backendsList);

            // Fake endpoints repo
            this.Mock<IBackendEndpointsRepo>()
                .Setup(m => m.SetEndpointsAsync(It.IsAny<string>(), It.IsAny<IList<BackendEndpoint>>(), It.IsAny<CancellationToken>()))
                .Callback((string backendId, IList<BackendEndpoint> endpointList, CancellationToken token) => this.endpointsRepo[backendId] = endpointList);
            this.Mock<IBackendEndpointsRepo>()
                .Setup(m => m.RemoveEndpointsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string backendId, CancellationToken token) => this.endpointsRepo.Remove(backendId));
        }

        [Fact]
        public async void ExecuteAsync_NoAppsDiscovered_NoBackends()
        {
            // Setup
            this.Mock_AppsResponse();

            // Act
            var worker = this.Create<ServiceFabricDiscoveryWorker>();
            await worker.ExecuteAsync(CancellationToken.None);

            // Assert
            this.backendsRepo.Should().BeEmpty();
        }

        [Fact]
        public async void ExecuteAsync_NoExtensions_NoBackends()
        {
            // Setup
            ApplicationWrapper application;
            this.Mock_AppsResponse(application = this.CreateApp_1Service_SingletonPartition_1Replica("MyCoolApp", "MyAwesomeService", out ServiceWrapper service, out ReplicaWrapper replica));
            this.Mock_ServiceLabels(application, service, new Dictionary<string, string>());

            // Act
            var worker = this.Create<ServiceFabricDiscoveryWorker>();
            await worker.ExecuteAsync(CancellationToken.None);

            // Assert
            this.backendsRepo.Should().BeEmpty();
        }

        [Fact]
        public async void ExecuteAsync_GatewayNotEnabled_NoBackends()
        {
            // Setup
            ApplicationWrapper application;
            this.Mock_AppsResponse(application = this.CreateApp_1Service_SingletonPartition_1Replica("MyCoolApp", "MyAwesomeService", out ServiceWrapper service, out ReplicaWrapper replica));
            this.Mock_ServiceLabels(application, service, new Dictionary<string, string>() { { "IslandGateway.Enable", "false" } });

            // Act
            var worker = this.Create<ServiceFabricDiscoveryWorker>();
            await worker.ExecuteAsync(CancellationToken.None);

            // Assert
            this.backendsRepo.Should().BeEmpty();
        }

        [Fact]
        public async void ExecuteAsync_SingleServiceWithGatewayEnabled_OneBackendFound()
        {
            // Setup
            const string TestBackendId = "My Backend ID 123";
            var labels = SFTestHelpers.DummyLabels(TestBackendId);
            ApplicationWrapper application, anotherApplication;
            this.Mock_AppsResponse(
                application = this.CreateApp_1Service_2Partition_2ReplicasEach("MyApp", "MYService", out ServiceWrapper service, out List<ReplicaWrapper> replicas),
                anotherApplication = this.CreateApp_1Service_2Partition_2ReplicasEach("AnotherApp", "AnotherService", out ServiceWrapper anotherService, out List<ReplicaWrapper> otherReplicas));
            this.Mock_ServiceLabels(application, service, labels);
            this.Mock_ServiceLabels(anotherApplication, anotherService, new Dictionary<string, string>());

            // Act
            var worker = this.Create<ServiceFabricDiscoveryWorker>();
            await worker.ExecuteAsync(CancellationToken.None);

            // Assert
            var expectedBackends = new List<Backend>
            {
                LabelsParser.BuildBackend(labels),
            };
            var expectedEndpoints = new Dictionary<string, IList<BackendEndpoint>>
            {
                {
                    TestBackendId,
                    new List<BackendEndpoint>
                    {
                        SFTestHelpers.BuildEndpointFromReplica(replicas[0]),
                        SFTestHelpers.BuildEndpointFromReplica(replicas[1]),
                        SFTestHelpers.BuildEndpointFromReplica(replicas[2]),
                        SFTestHelpers.BuildEndpointFromReplica(replicas[3]),
                    }
                },
            };

            this.backendsRepo.Should().BeEquivalentTo(expectedBackends);

            foreach (var keyval in expectedEndpoints)
            {
                var endpoints = this.endpointsRepo[keyval.Key];
                endpoints.Should().BeEquivalentTo(keyval.Value);
            }
        }

        [Fact]
        public async void ExecuteAsync_MultipleServicesWithGatewayEnabled_MultipleBackendsFound()
        {
            // Setup
            const string TestBackendIdApp1Sv1 = "My Backend ID 123";
            const string TestBackendIdApp1Sv2 = "Another Backend ID 234";
            const string TestBackendIdApp2Sv3 = "Yet Another Backend ID 456";
            var labels1 = SFTestHelpers.DummyLabels(TestBackendIdApp1Sv1);
            var labels2 = SFTestHelpers.DummyLabels(TestBackendIdApp1Sv2);
            var labels3 = SFTestHelpers.DummyLabels(TestBackendIdApp2Sv3);
            ApplicationWrapper application1, application2;
            this.Mock_AppsResponse(
                application1 = this.CreateApp_1Service_SingletonPartition_1Replica("MyApp", "MYService", out ServiceWrapper service1, out ReplicaWrapper replica1),
                application2 = this.CreateApp_2Service_SingletonPartition_1Replica("MyApp2", "MyService2", "MyService3", out ServiceWrapper service2, out ServiceWrapper service3, out ReplicaWrapper replica2, out ReplicaWrapper replica3));

            this.Mock_ServiceLabels(application1, service1, labels1);
            this.Mock_ServiceLabels(application2, service2, labels2);
            this.Mock_ServiceLabels(application2, service3, labels3);

            // Act
            var worker = this.Create<ServiceFabricDiscoveryWorker>();
            await worker.ExecuteAsync(CancellationToken.None);

            // Assert
            var expectedBackends = new List<Backend>
            {
                LabelsParser.BuildBackend(labels1),
                LabelsParser.BuildBackend(labels2),
                LabelsParser.BuildBackend(labels3),
            };
            var expectedEndpoints = new Dictionary<string, IList<BackendEndpoint>>
            {
                {
                    TestBackendIdApp1Sv1,
                    new List<BackendEndpoint> { SFTestHelpers.BuildEndpointFromReplica(replica1) }
                },
                {
                    TestBackendIdApp1Sv2,
                    new List<BackendEndpoint> { SFTestHelpers.BuildEndpointFromReplica(replica2) }
                },
                {
                    TestBackendIdApp2Sv3,
                    new List<BackendEndpoint> { SFTestHelpers.BuildEndpointFromReplica(replica3) }
                },
            };

            this.backendsRepo.Should().BeEquivalentTo(expectedBackends);

            foreach (var keyval in expectedEndpoints)
            {
                var endpoints = this.endpointsRepo[keyval.Key];
                endpoints.Should().BeEquivalentTo(keyval.Value);
            }
        }

        // Mocking helpers
        private ApplicationWrapper CreateApp_1Service_SingletonPartition_1Replica(
            string appTypeName,
            string serviceTypeName,
            out ServiceWrapper service,
            out ReplicaWrapper replica)
        {
            List<ReplicaWrapper> replicas;
            service = this.CreateService(appTypeName, serviceTypeName, 1, 1, out replicas);
            replica = replicas[0];
            this.Mock_ServicesResponse(new Uri($"fabric:/{appTypeName}"), service);
            return SFTestHelpers.FakeApp(appTypeName, appTypeName);
        }
        private ApplicationWrapper CreateApp_1Service_2Partition_2ReplicasEach(
            string appTypeName,
            string serviceTypeName,
            out ServiceWrapper service,
            out List<ReplicaWrapper> replicas)
        {
            service = this.CreateService(appTypeName, serviceTypeName, 2, 2, out replicas);
            this.Mock_ServicesResponse(new Uri($"fabric:/{appTypeName}"), service);
            return SFTestHelpers.FakeApp(appTypeName, appTypeName);
        }
        private ApplicationWrapper CreateApp_2Service_SingletonPartition_1Replica(
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
        private ServiceWrapper CreateService(string appTypeName, string serviceTypeName, int numPartitions, int numReplicasPerPartition, out List<ReplicaWrapper> replicas)
        {
            var svcName = new Uri($"fabric:/{appTypeName}/{serviceTypeName}");
            var service = SFTestHelpers.FakeService(svcName, $"{appTypeName}_{serviceTypeName}_Type");
            replicas = new List<ReplicaWrapper>();

            var partitions = new List<Guid>();
            for (int i = 1; i <= numPartitions; i++)
            {
                var partitionReplicas = Enumerable.Range(1, numReplicasPerPartition).Select(replicaId => SFTestHelpers.FakeReplica(svcName, replicaId)).ToList();
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
                .Setup(
                    m => m.GetExtensionLabelsAsync(
                        application.ApplicationTypeName,
                        application.ApplicationTypeVersion,
                        service.ServiceTypeName,
                        service.ServiceName.ToString(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(labels);
        }
    }
}
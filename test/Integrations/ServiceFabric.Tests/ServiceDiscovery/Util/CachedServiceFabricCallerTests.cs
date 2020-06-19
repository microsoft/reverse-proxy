// <copyright file="CachedServiceFabricCallerTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Abstractions.Time;
using Microsoft.ReverseProxy.Telemetry;
using Moq;
using Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration.Tests
{
    public class CachedServiceFabricCallerTests : TestAutoMockBase
    {
        // TODO: add tests to verify the cache keys are correct (that is, different arguments are cached separately)
        private VirtualMonotonicTimer timer;
        public CachedServiceFabricCallerTests(ITestOutputHelper testOutputHelper)
        {
            this.timer = new VirtualMonotonicTimer();
            this.Provide<IMonotonicTimer>(this.timer);
            this.Provide<ILogger>(new XunitLogger<ServiceFabricDiscoveryWorker>(testOutputHelper));
            this.Provide<IOperationLogger<CachedServiceFabricCaller>>(new NullOperationLogger<CachedServiceFabricCaller>());
        }

        [Fact]
        public async void GetApplicationListAsync_ServiceFabricFails_ResultIsCached()
        {
            // Arrange
            var caller = this.Create<CachedServiceFabricCaller>();
            var originalApps = new List<ApplicationWrapper> { SFTestHelpers.FakeApp("MyApp") };

            this.Mock<IQueryClientWrapper>()
                .SetupSequence(m => m.GetApplicationListAsync(
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalApps)
                .ThrowsAsync(new Exception("the cake is a lie"))
                .ThrowsAsync(new Exception("the cake is still a lie"));

            // Act & Assert
            await this.CallThreeTimesAndAssertAsync(() => caller.GetApplicationListAsync(CancellationToken.None));
        }
        [Fact]
        public async void GetServiceListAsync_ServiceFabricFails_ResultIsCached()
        {
            // Arrange
            var caller = this.Create<CachedServiceFabricCaller>();
            var originalServices = new List<ServiceWrapper> { SFTestHelpers.FakeService(new Uri("http://localhost/app1/sv1"), "MyServiceType") };
            this.Mock<IQueryClientWrapper>()
                .SetupSequence(m => m.GetServiceListAsync(
                    new Uri("http://localhost/app1"),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalServices.ToList())
                .ThrowsAsync(new Exception("the cake is a lie"))
                .ThrowsAsync(new Exception("the cake is still a lie"));

            // Act
            await this.CallThreeTimesAndAssertAsync(() => caller.GetServiceListAsync(new Uri("http://localhost/app1"), CancellationToken.None));
        }
        [Fact]
        public async void GetPartitionListAsync_ServiceFabricFails_ResultIsCached()
        {
            // Arrange
            var caller = this.Create<CachedServiceFabricCaller>();
            var originalPartitionIds = new List<Guid> { Guid.NewGuid() };
            this.Mock<IQueryClientWrapper>()
                .SetupSequence(m => m.GetPartitionListAsync(
                    new Uri("http://localhost/app1/sv1"),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalPartitionIds)
                .ThrowsAsync(new Exception("the cake is a lie"))
                .ThrowsAsync(new Exception("the cake is still a lie"));

            // Act & Assert
            await this.CallThreeTimesAndAssertAsync(() => caller.GetPartitionListAsync(new Uri("http://localhost/app1/sv1"), CancellationToken.None));
        }
        [Fact]
        public async void GetReplicaListAsync_ServiceFabricFails_ResultIsCached()
        {
            // Arrange
            var caller = this.Create<CachedServiceFabricCaller>();
            var partitionId = Guid.NewGuid();
            var originalReplicas = new List<ReplicaWrapper> { SFTestHelpers.FakeReplica(new Uri("http://localhost/app1/sv1"), 1) };
            this.Mock<IQueryClientWrapper>()
                .SetupSequence(m => m.GetReplicaListAsync(
                    partitionId,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalReplicas)
                .ThrowsAsync(new Exception("the cake is a lie"))
                .ThrowsAsync(new Exception("the cake is still a lie"));

            // Act & Assert
            await this.CallThreeTimesAndAssertAsync(() => caller.GetReplicaListAsync(partitionId, CancellationToken.None));
        }

        [Fact]
        public async void GetServiceManifestName_ServiceFabricFails_ResultIsCached()
        {
            // Arrange
            var caller = this.Create<CachedServiceFabricCaller>();
            string originalServiceManifestName = "MyCoolManifest";
            this.Mock<IQueryClientWrapper>()
                .SetupSequence(
                    m => m.GetServiceManifestName(
                        "AppName",
                        "1.2.3",
                        "MyServiceType",
                        It.IsAny<TimeSpan>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalServiceManifestName)
                .ThrowsAsync(new Exception("the cake is a lie"))
                .ThrowsAsync(new Exception("the cake is still a lie"));

            // Act & Assert
            await this.CallThreeTimesAndAssertAsync(() => caller.GetServiceManifestName("AppName", "1.2.3", "MyServiceType", CancellationToken.None));
        }
        [Fact]
        public async void GetServiceManifestAsync_ServiceFabricFails_ResultIsCached()
        {
            // Arrange
            var caller = this.Create<CachedServiceFabricCaller>();
            string originalRawServiceManifest = "<xml> </xml>";
            this.Mock<IServiceManagementClientWrapper>()
                .SetupSequence(
                    m => m.GetServiceManifestAsync(
                        "AppName",
                        "1.2.3",
                        "MyCoolManifest",
                        It.IsAny<TimeSpan>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalRawServiceManifest)
                .ThrowsAsync(new Exception("the cake is a lie"))
                .ThrowsAsync(new Exception("the cake is still a lie"));

            // Act & Assert
            await this.CallThreeTimesAndAssertAsync(() => caller.GetServiceManifestAsync("AppName", "1.2.3", "MyCoolManifest", CancellationToken.None));
        }
        [Fact]
        public async void EnumeratePropertiesAsync_ServiceFabricFails_ResultIsCached()
        {
            // Arrange
            var caller = this.Create<CachedServiceFabricCaller>();
            var originalProperties = new Dictionary<string, string> { { "key", "value" } };
            this.Mock<IPropertyManagementClientWrapper>()
                .SetupSequence(
                    m => m.EnumeratePropertiesAsync(
                        new Uri("http://localhost/app1/sv1"),
                        It.IsAny<TimeSpan>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalProperties)
                .ThrowsAsync(new Exception("the cake is a lie"))
                .ThrowsAsync(new Exception("the cake is still a lie"));

            // Act & Assert
            await this.CallThreeTimesAndAssertAsync(() => caller.EnumeratePropertiesAsync(new Uri("http://localhost/app1/sv1"), CancellationToken.None));
        }

        private async Task CallThreeTimesAndAssertAsync<T>(Func<Task<T>> call)
        {
            TimeSpan almostExpirationOffset = CachedServiceFabricCaller.CacheExpirationOffset.Subtract(TimeSpan.FromTicks(1));

            // Act
            var firstResult = await call(); // First call is successful
            this.timer.AdvanceClockBy(almostExpirationOffset);
            var secondResult = await call(); // Second call should use last result from cache
            this.timer.AdvanceClockBy(almostExpirationOffset);

            // Assert
            secondResult.Should().BeEquivalentTo(firstResult);
            await call.Should().ThrowAsync<Exception>(); // Third call fails
        }
    }
}

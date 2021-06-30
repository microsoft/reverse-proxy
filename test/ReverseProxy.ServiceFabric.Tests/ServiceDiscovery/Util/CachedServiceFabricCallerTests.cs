// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Yarp.ReverseProxy.Common.Tests;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.ServiceFabric.Tests
{
    public class CachedServiceFabricCallerTests : TestAutoMockBase
    {
        // TODO: add tests to verify the cache keys are correct (that is, different arguments are cached separately)
        private readonly ManualClock _clock;

        public CachedServiceFabricCallerTests(ITestOutputHelper testOutputHelper)
        {
            _clock = new ManualClock();
            Provide<IClock>(_clock);
            Provide<ILogger>(new XunitLogger<Discoverer>(testOutputHelper));
        }

        [Fact]
        public async void GetApplicationListAsync_ServiceFabricFails_ResultIsCached()
        {
            var caller = Create<CachedServiceFabricCaller>();
            var originalApps = new List<ApplicationWrapper> { SFTestHelpers.FakeApp("MyApp") };

            Mock<IQueryClientWrapper>()
                .SetupSequence(m => m.GetApplicationListAsync(
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalApps)
                .ThrowsAsync(new Exception("the cake is a lie"))
                .ThrowsAsync(new Exception("the cake is still a lie"));

            await CallThreeTimesAndAssertAsync(() => caller.GetApplicationListAsync(CancellationToken.None));
        }
        [Fact]
        public async void GetServiceListAsync_ServiceFabricFails_ResultIsCached()
        {
            var caller = Create<CachedServiceFabricCaller>();
            var originalServices = new List<ServiceWrapper> { SFTestHelpers.FakeService(new Uri("http://localhost/app1/sv1"), "MyServiceType") };
            Mock<IQueryClientWrapper>()
                .SetupSequence(m => m.GetServiceListAsync(
                    new Uri("http://localhost/app1"),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalServices.ToList())
                .ThrowsAsync(new Exception("the cake is a lie"))
                .ThrowsAsync(new Exception("the cake is still a lie"));

            await CallThreeTimesAndAssertAsync(() => caller.GetServiceListAsync(new Uri("http://localhost/app1"), CancellationToken.None));
        }
        [Fact]
        public async void GetPartitionListAsync_ServiceFabricFails_ResultIsCached()
        {
            var caller = Create<CachedServiceFabricCaller>();
            var originalPartitionInfoList = new List<PartitionWrapper> { SFTestHelpers.FakePartition() };
            Mock<IQueryClientWrapper>()
                .SetupSequence(m => m.GetPartitionListAsync(
                    new Uri("http://localhost/app1/sv1"),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalPartitionInfoList)
                .ThrowsAsync(new Exception("the cake is a lie"))
                .ThrowsAsync(new Exception("the cake is still a lie"));

            await CallThreeTimesAndAssertAsync(() => caller.GetPartitionListAsync(new Uri("http://localhost/app1/sv1"), CancellationToken.None));
        }
        [Fact]
        public async void GetReplicaListAsync_ServiceFabricFails_ResultIsCached()
        {
            var caller = Create<CachedServiceFabricCaller>();
            var partitionId = Guid.NewGuid();
            var originalReplicas = new List<ReplicaWrapper> { SFTestHelpers.FakeReplica(new Uri("http://localhost/app1/sv1"), 1) };
            Mock<IQueryClientWrapper>()
                .SetupSequence(m => m.GetReplicaListAsync(
                    partitionId,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalReplicas)
                .ThrowsAsync(new Exception("the cake is a lie"))
                .ThrowsAsync(new Exception("the cake is still a lie"));

            await CallThreeTimesAndAssertAsync(() => caller.GetReplicaListAsync(partitionId, CancellationToken.None));
        }

        [Fact]
        public async void GetServiceManifestName_ServiceFabricFails_ResultIsCached()
        {
            var caller = Create<CachedServiceFabricCaller>();
            var originalServiceManifestName = "MyCoolManifest";
            Mock<IQueryClientWrapper>()
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

            await CallThreeTimesAndAssertAsync(() => caller.GetServiceManifestName("AppName", "1.2.3", "MyServiceType", CancellationToken.None));
        }
        [Fact]
        public async void GetServiceManifestAsync_ServiceFabricFails_ResultIsCached()
        {
            var caller = Create<CachedServiceFabricCaller>();
            var originalRawServiceManifest = "<xml> </xml>";
            Mock<IServiceManagementClientWrapper>()
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

            await CallThreeTimesAndAssertAsync(() => caller.GetServiceManifestAsync("AppName", "1.2.3", "MyCoolManifest", CancellationToken.None));
        }
        [Fact]
        public async void EnumeratePropertiesAsync_ServiceFabricFails_ResultIsCached()
        {
            var caller = Create<CachedServiceFabricCaller>();
            var originalProperties = new Dictionary<string, string> { { "key", "value" } };
            Mock<IPropertyManagementClientWrapper>()
                .SetupSequence(
                    m => m.EnumeratePropertiesAsync(
                        new Uri("http://localhost/app1/sv1"),
                        It.IsAny<TimeSpan>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalProperties)
                .ThrowsAsync(new Exception("the cake is a lie"))
                .ThrowsAsync(new Exception("the cake is still a lie"));

            await CallThreeTimesAndAssertAsync(() => caller.EnumeratePropertiesAsync(new Uri("http://localhost/app1/sv1"), CancellationToken.None));
        }

        private async Task CallThreeTimesAndAssertAsync<T>(Func<Task<T>> call)
        {
            var almostExpirationOffset = CachedServiceFabricCaller.CacheExpirationOffset.Subtract(TimeSpan.FromTicks(1));

            var firstResult = await call(); // First call is successful
            _clock.AdvanceClockBy(almostExpirationOffset);
            var secondResult = await call(); // Second call should use last result from cache
            _clock.AdvanceClockBy(almostExpirationOffset);

            secondResult.Should().BeEquivalentTo(firstResult);
            await call.Should().ThrowAsync<Exception>(); // Third call fails
        }
    }
}

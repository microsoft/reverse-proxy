// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Utilities;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    public class TransportFailureRateHealthPolicyTests
    {
        [Fact]
        public void RequestProxied_FailureRateLimitExceeded_MarkDestinationUnhealthy()
        {
            var options = Options.Create(
                new TransportFailureRateHealthPolicyOptions { DefaultFailureRateLimit = 0.5, DetectionWindowSize = TimeSpan.FromSeconds(30), MinimalTotalCountThreshold = 1 });
            var clock = new UptimeClockStub { TickCount = 10000 };
            var reactivationScheduler = new Mock<IReactivationScheduler>();
            var policy = new TransportFailureRateHealthPolicy(options, clock, reactivationScheduler.Object);
            Assert.Equal(HealthCheckConstants.PassivePolicy.TransportFailureRate, policy.Name);

            var cluster0 = GetClusterInfo("cluster0", destinationCount: 2);
            var cluster1 = GetClusterInfo("cluster1", destinationCount: 2, failureRateLimit: 0.61, reactivationPeriod: TimeSpan.FromSeconds(100));

            // Initial state
            Assert.All(cluster0.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Unknown, d.DynamicState.Health.Passive));
            Assert.All(cluster1.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Unknown, d.DynamicState.Health.Passive));

            // Successful requests
            for (var i = 0; i < 3; i++)
            {
                policy.RequestProxied(cluster0, cluster0.DestinationManager.Items.Value[0], new DefaultHttpContext());
                policy.RequestProxied(cluster0, cluster0.DestinationManager.Items.Value[1], new DefaultHttpContext());
                policy.RequestProxied(cluster1, cluster1.DestinationManager.Items.Value[0], new DefaultHttpContext());
                policy.RequestProxied(cluster1, cluster1.DestinationManager.Items.Value[1], new DefaultHttpContext());
                clock.TickCount += 4000;
            }

            Assert.All(cluster0.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Healthy, d.DynamicState.Health.Passive));
            Assert.All(cluster1.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Healthy, d.DynamicState.Health.Passive));

            // Failed requests
            for (var i = 0; i < 3; i++)
            {
                policy.RequestProxied(cluster0, cluster0.DestinationManager.Items.Value[1], GetFailedRequestContext(ProxyError.RequestTimedOut));
                policy.RequestProxied(cluster1, cluster1.DestinationManager.Items.Value[0], GetFailedRequestContext(ProxyError.Request));
                clock.TickCount += 4000;
            }

            Assert.Equal(DestinationHealth.Healthy, cluster0.DestinationManager.Items.Value[0].DynamicState.Health.Passive);
            Assert.Equal(DestinationHealth.Unhealthy, cluster0.DestinationManager.Items.Value[1].DynamicState.Health.Passive);
            Assert.All(cluster1.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Healthy, d.DynamicState.Health.Passive));

            reactivationScheduler.Verify(s => s.Schedule(cluster0.DestinationManager.Items.Value[1], TimeSpan.FromSeconds(60)), Times.Once);
            reactivationScheduler.VerifyNoOtherCalls();

            // Two more failed requests
            policy.RequestProxied(cluster1, cluster1.DestinationManager.Items.Value[0], GetFailedRequestContext(ProxyError.Request));
            // End of the detection window
            clock.TickCount += 6000;
            policy.RequestProxied(cluster1, cluster1.DestinationManager.Items.Value[0], GetFailedRequestContext(ProxyError.Request));

            Assert.Equal(DestinationHealth.Unhealthy, cluster1.DestinationManager.Items.Value[0].DynamicState.Health.Passive);
            Assert.Equal(DestinationHealth.Healthy, cluster1.DestinationManager.Items.Value[1].DynamicState.Health.Passive);

            reactivationScheduler.Verify(s => s.Schedule(cluster1.DestinationManager.Items.Value[0], TimeSpan.FromSeconds(100)), Times.Once);
            reactivationScheduler.VerifyNoOtherCalls();

            Assert.All(cluster0.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Unknown, d.DynamicState.Health.Active));
            Assert.All(cluster1.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Unknown, d.DynamicState.Health.Active));
        }

        [Fact]
        public void RequestProxied_FailureMovedOutOfDetectionWindow_MarkDestinationHealthy()
        {
            var options = Options.Create(
                new TransportFailureRateHealthPolicyOptions { DefaultFailureRateLimit = 0.5, DetectionWindowSize = TimeSpan.FromSeconds(30), MinimalTotalCountThreshold = 1 });
            var clock = new UptimeClockStub { TickCount = 10000 };
            var reactivationScheduler = new Mock<IReactivationScheduler>();
            var policy = new TransportFailureRateHealthPolicy(options, clock, reactivationScheduler.Object);

            var cluster = GetClusterInfo("cluster0", destinationCount: 2);

            // Initial state
            Assert.All(cluster.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Unknown, d.DynamicState.Health.Passive));

            // Initial failed requests
            for (var i = 0; i < 2; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items.Value[1], GetFailedRequestContext(ProxyError.RequestTimedOut));
                clock.TickCount += 1000;
            }

            Assert.Equal(DestinationHealth.Unknown, cluster.DestinationManager.Items.Value[0].DynamicState.Health.Passive);
            Assert.Equal(DestinationHealth.Unhealthy, cluster.DestinationManager.Items.Value[1].DynamicState.Health.Passive);

            // Successful requests
            for (var i = 0; i < 4; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items.Value[0], new DefaultHttpContext());
                policy.RequestProxied(cluster, cluster.DestinationManager.Items.Value[1], new DefaultHttpContext());
                clock.TickCount += 5000;
            }

            Assert.All(cluster.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Healthy, d.DynamicState.Health.Passive));

            // Failed requests
            for (var i = 0; i < 2; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items.Value[1], GetFailedRequestContext(ProxyError.RequestTimedOut));
                clock.TickCount += 1;
            }

            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items.Value[0].DynamicState.Health.Passive);
            Assert.Equal(DestinationHealth.Unhealthy, cluster.DestinationManager.Items.Value[1].DynamicState.Health.Passive);

            // Shift the detection window to the future
            clock.TickCount += 10998;

            // New failed request, but 2 oldest failures have moved out of the detection window
            policy.RequestProxied(cluster, cluster.DestinationManager.Items.Value[1], GetFailedRequestContext(ProxyError.RequestTimedOut));

            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items.Value[0].DynamicState.Health.Passive);
            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items.Value[1].DynamicState.Health.Passive);
        }

        [Fact]
        public void RequestProxied_MultipleConcurrentRequests_MarkDestinationUnhealthyAndHealthyAgain()
        {
            var options = Options.Create(
                new TransportFailureRateHealthPolicyOptions { DefaultFailureRateLimit = 0.5, DetectionWindowSize = TimeSpan.FromSeconds(30), MinimalTotalCountThreshold = 1 });
            var clock = new UptimeClockStub { TickCount = 10000 };
            var reactivationScheduler = new Mock<IReactivationScheduler>();
            var reactivationPeriod = TimeSpan.FromSeconds(15);
            var policy = new TransportFailureRateHealthPolicy(options, clock, reactivationScheduler.Object);

            var cluster = GetClusterInfo("cluster0", destinationCount: 2, reactivationPeriod: reactivationPeriod);

            // Initial state
            Assert.All(cluster.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Unknown, d.DynamicState.Health.Passive));

            // Initial sucessful requests
            for (var i = 0; i < 2; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items.Value[1], new DefaultHttpContext());
            }

            Assert.Equal(DestinationHealth.Unknown, cluster.DestinationManager.Items.Value[0].DynamicState.Health.Passive);
            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items.Value[1].DynamicState.Health.Passive);
            reactivationScheduler.VerifyNoOtherCalls();

            // Concurrent failed requests.
            // They are 'concurrent' because the clock is not updated.
            for (var i = 0; i < 2; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items.Value[1], GetFailedRequestContext(ProxyError.RequestTimedOut));
            }

            Assert.Equal(DestinationHealth.Unknown, cluster.DestinationManager.Items.Value[0].DynamicState.Health.Passive);
            Assert.Equal(DestinationHealth.Unhealthy, cluster.DestinationManager.Items.Value[1].DynamicState.Health.Passive);
            reactivationScheduler.Verify(s => s.Schedule(cluster.DestinationManager.Items.Value[1], reactivationPeriod), Times.Once);
            reactivationScheduler.VerifyNoOtherCalls();

            // More successful requests
            for (var i = 0; i < 2; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items.Value[1], new DefaultHttpContext());
                clock.TickCount += 100;
            }

            Assert.Equal(DestinationHealth.Unknown, cluster.DestinationManager.Items.Value[0].DynamicState.Health.Passive);
            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items.Value[1].DynamicState.Health.Passive);
            reactivationScheduler.VerifyNoOtherCalls();

            // More failed requests
            for (var i = 0; i < 2; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items.Value[1], GetFailedRequestContext(ProxyError.RequestTimedOut));
                clock.TickCount += 100;
            }

            Assert.Equal(DestinationHealth.Unknown, cluster.DestinationManager.Items.Value[0].DynamicState.Health.Passive);
            Assert.Equal(DestinationHealth.Unhealthy, cluster.DestinationManager.Items.Value[1].DynamicState.Health.Passive);
            reactivationScheduler.Verify(s => s.Schedule(cluster.DestinationManager.Items.Value[1], reactivationPeriod), Times.Exactly(2));
            reactivationScheduler.VerifyNoOtherCalls();

            policy.RequestProxied(cluster, cluster.DestinationManager.Items.Value[0], new DefaultHttpContext());

            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items.Value[0].DynamicState.Health.Passive);
            reactivationScheduler.VerifyNoOtherCalls();
        }

        private HttpContext GetFailedRequestContext(ProxyError error)
        {
            var errorFeature = new ProxyErrorFeature(error, null);
            var context = new DefaultHttpContext();
            context.Features.Set<IProxyErrorFeature>(errorFeature);
            return context;
        }

        private ClusterInfo GetClusterInfo(string id, int destinationCount, double? failureRateLimit = null, TimeSpan? reactivationPeriod = null)
        {
            var metadata = failureRateLimit != null
                ? new Dictionary<string, string> { { TransportFailureRateHealthPolicyOptions.FailureRateLimitMetadataName, failureRateLimit.ToString() } }
                : null;
            var clusterConfig = new ClusterConfig(
                new Cluster { Id = id },
                new ClusterHealthCheckOptions(new ClusterPassiveHealthCheckOptions(true, "policy", reactivationPeriod), default),
                default,
                default,
                null,
                default,
                metadata);
            var clusterInfo = new ClusterInfo(id, new DestinationManager());
            clusterInfo.ConfigSignal.Value = clusterConfig;
            for (var i = 0; i < destinationCount; i++)
            {
                var destinationConfig = new DestinationConfig($"https://localhost:1000{i}/{id}/", $"https://localhost:2000{i}/{id}/");
                var destinationId = $"destination{i}";
                clusterInfo.DestinationManager.GetOrCreateItem(destinationId, d =>
                {
                    d.ConfigSignal.Value = destinationConfig;
                });
            }

            return clusterInfo;
        }

        private class UptimeClockStub : IUptimeClock
        {
            public long TickCount { get; set; }
        }
    }
}

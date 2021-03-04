// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy;
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
            var clock = new ManualClock(TimeSpan.FromMilliseconds(10000));
            var healthUpdater = new Mock<IDestinationHealthUpdater>();
            var policy = new TransportFailureRateHealthPolicy(options, clock, healthUpdater.Object);
            Assert.Equal(HealthCheckConstants.PassivePolicy.TransportFailureRate, policy.Name);

            var reactivationPeriod0 = TimeSpan.FromSeconds(60);
            var reactivationPeriod1 = TimeSpan.FromSeconds(100);
            var cluster0 = GetClusterInfo("cluster0", destinationCount: 2);
            var cluster1 = GetClusterInfo("cluster1", destinationCount: 2, failureRateLimit: 0.61, reactivationPeriod1);

            // Initial state
            Assert.All(cluster0.DestinationManager.Items, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Passive));
            Assert.All(cluster1.DestinationManager.Items, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Passive));

            // Successful requests
            for (var i = 0; i < 3; i++)
            {
                policy.RequestProxied(cluster0, cluster0.DestinationManager.Items[0], new DefaultHttpContext());
                policy.RequestProxied(cluster0, cluster0.DestinationManager.Items[1], new DefaultHttpContext());
                policy.RequestProxied(cluster1, cluster1.DestinationManager.Items[0], new DefaultHttpContext());
                policy.RequestProxied(cluster1, cluster1.DestinationManager.Items[1], new DefaultHttpContext());
                clock.AdvanceClockBy(TimeSpan.FromMilliseconds(4000));
            }

            healthUpdater.Verify(u => u.SetPassive(cluster0, cluster0.DestinationManager.Items[0], DestinationHealth.Healthy, reactivationPeriod0), Times.Exactly(3));
            healthUpdater.Verify(u => u.SetPassive(cluster0, cluster0.DestinationManager.Items[1], DestinationHealth.Healthy, reactivationPeriod0), Times.Exactly(3));
            healthUpdater.Verify(u => u.SetPassive(cluster1, cluster1.DestinationManager.Items[0], DestinationHealth.Healthy, reactivationPeriod1), Times.Exactly(3));
            healthUpdater.Verify(u => u.SetPassive(cluster1, cluster1.DestinationManager.Items[1], DestinationHealth.Healthy, reactivationPeriod1), Times.Exactly(3));
            healthUpdater.VerifyNoOtherCalls();

            // Failed requests
            for (var i = 0; i < 3; i++)
            {
                policy.RequestProxied(cluster0, cluster0.DestinationManager.Items[1], GetFailedRequestContext(ProxyError.RequestTimedOut));
                policy.RequestProxied(cluster1, cluster1.DestinationManager.Items[0], GetFailedRequestContext(ProxyError.Request));
                clock.AdvanceClockBy(TimeSpan.FromMilliseconds(4000));
            }

            healthUpdater.Verify(u => u.SetPassive(cluster0, cluster0.DestinationManager.Items[1], DestinationHealth.Healthy, reactivationPeriod0), Times.Exactly(5));
            healthUpdater.Verify(u => u.SetPassive(cluster0, cluster0.DestinationManager.Items[1], DestinationHealth.Unhealthy, reactivationPeriod0), Times.Once);
            healthUpdater.Verify(u => u.SetPassive(cluster1, cluster1.DestinationManager.Items[0], DestinationHealth.Healthy, reactivationPeriod1), Times.Exactly(6));
            healthUpdater.VerifyNoOtherCalls();

            // Two more failed requests
            policy.RequestProxied(cluster1, cluster1.DestinationManager.Items[0], GetFailedRequestContext(ProxyError.Request));
            // End of the detection window
            clock.AdvanceClockBy(TimeSpan.FromMilliseconds(6000));
            policy.RequestProxied(cluster1, cluster1.DestinationManager.Items[0], GetFailedRequestContext(ProxyError.Request));

            healthUpdater.Verify(u => u.SetPassive(cluster1, cluster1.DestinationManager.Items[0], DestinationHealth.Healthy, reactivationPeriod1), Times.Exactly(7));
            healthUpdater.Verify(u => u.SetPassive(cluster1, cluster1.DestinationManager.Items[0], DestinationHealth.Unhealthy, reactivationPeriod1), Times.Once);
            healthUpdater.VerifyNoOtherCalls();
        }

        [Fact]
        public void RequestProxied_FailureMovedOutOfDetectionWindow_MarkDestinationHealthy()
        {
            var options = Options.Create(
                new TransportFailureRateHealthPolicyOptions { DefaultFailureRateLimit = 0.5, DetectionWindowSize = TimeSpan.FromSeconds(30), MinimalTotalCountThreshold = 1 });
            var clock = new ManualClock(TimeSpan.FromMilliseconds(10000));
            var healthUpdater = new Mock<IDestinationHealthUpdater>();
            var policy = new TransportFailureRateHealthPolicy(options, clock, healthUpdater.Object);

            var cluster = GetClusterInfo("cluster0", destinationCount: 2);

            // Initial failed requests
            for (var i = 0; i < 2; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items[1], GetFailedRequestContext(ProxyError.RequestTimedOut));
                clock.AdvanceClockBy(TimeSpan.FromMilliseconds(1000));
            }

            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Unhealthy, TimeSpan.FromSeconds(60)), Times.Exactly(2));
            healthUpdater.VerifyNoOtherCalls();

            // Successful requests
            for (var i = 0; i < 4; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items[0], new DefaultHttpContext());
                policy.RequestProxied(cluster, cluster.DestinationManager.Items[1], new DefaultHttpContext());
                clock.AdvanceClockBy(TimeSpan.FromMilliseconds(5000));
            }

            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[0], DestinationHealth.Healthy, TimeSpan.FromSeconds(60)), Times.Exactly(4));
            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Healthy, TimeSpan.FromSeconds(60)), Times.Exactly(2));
            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Unhealthy, TimeSpan.FromSeconds(60)), Times.Exactly(4));
            healthUpdater.VerifyNoOtherCalls();

            // Failed requests
            for (var i = 0; i < 2; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items[1], GetFailedRequestContext(ProxyError.RequestTimedOut));
                clock.AdvanceClockBy(TimeSpan.FromMilliseconds(1));
            }

            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Healthy, TimeSpan.FromSeconds(60)), Times.Exactly(3));
            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Unhealthy, TimeSpan.FromSeconds(60)), Times.Exactly(5));
            healthUpdater.VerifyNoOtherCalls();

            // Shift the detection window to the future
            clock.AdvanceClockBy(TimeSpan.FromMilliseconds(10998));

            // New failed request, but 2 oldest failures have moved out of the detection window
            policy.RequestProxied(cluster, cluster.DestinationManager.Items[1], GetFailedRequestContext(ProxyError.RequestTimedOut));

            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Healthy, TimeSpan.FromSeconds(60)), Times.Exactly(4));
            healthUpdater.VerifyNoOtherCalls();
        }

        [Fact]
        public void RequestProxied_MultipleConcurrentRequests_MarkDestinationUnhealthyAndHealthyAgain()
        {
            var options = Options.Create(
                new TransportFailureRateHealthPolicyOptions { DefaultFailureRateLimit = 0.5, DetectionWindowSize = TimeSpan.FromSeconds(30), MinimalTotalCountThreshold = 1 });
            var clock = new ManualClock(TimeSpan.FromMilliseconds(10000));
            var healthUpdater = new Mock<IDestinationHealthUpdater>();
            var reactivationPeriod = TimeSpan.FromSeconds(15);
            var policy = new TransportFailureRateHealthPolicy(options, clock, healthUpdater.Object);

            var cluster = GetClusterInfo("cluster0", destinationCount: 2, reactivationPeriod: reactivationPeriod);

            // Initial state
            Assert.All(cluster.DestinationManager.Items, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Passive));

            // Initial sucessful requests
            for (var i = 0; i < 2; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items[1], new DefaultHttpContext());
            }

            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Healthy, reactivationPeriod), Times.Exactly(2));
            healthUpdater.VerifyNoOtherCalls();

            // Concurrent failed requests.
            // They are 'concurrent' because the clock is not updated.
            for (var i = 0; i < 2; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items[1], GetFailedRequestContext(ProxyError.RequestTimedOut));
            }

            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Healthy, reactivationPeriod), Times.Exactly(3));
            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Unhealthy, reactivationPeriod), Times.Once);
            healthUpdater.VerifyNoOtherCalls();

            // More successful requests
            for (var i = 0; i < 2; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items[1], new DefaultHttpContext());
                clock.AdvanceClockBy(TimeSpan.FromMilliseconds(100));
            }

            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Healthy, reactivationPeriod), Times.Exactly(5));
            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Unhealthy, reactivationPeriod), Times.Once);
            healthUpdater.VerifyNoOtherCalls();

            // More failed requests
            for (var i = 0; i < 2; i++)
            {
                policy.RequestProxied(cluster, cluster.DestinationManager.Items[1], GetFailedRequestContext(ProxyError.RequestTimedOut));
                clock.AdvanceClockBy(TimeSpan.FromMilliseconds(100));
            }

            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Healthy, reactivationPeriod), Times.Exactly(6));
            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[1], DestinationHealth.Unhealthy, reactivationPeriod), Times.Exactly(2));
            healthUpdater.VerifyNoOtherCalls();

            policy.RequestProxied(cluster, cluster.DestinationManager.Items[0], new DefaultHttpContext());

            healthUpdater.Verify(u => u.SetPassive(cluster, cluster.DestinationManager.Items[0], DestinationHealth.Healthy, reactivationPeriod), Times.Once);
            healthUpdater.VerifyNoOtherCalls();
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
                ? new Dictionary<string, string> { { TransportFailureRateHealthPolicyOptions.FailureRateLimitMetadataName, failureRateLimit?.ToString(CultureInfo.InvariantCulture) } }
                : null;
            var clusterConfig = new ClusterConfig(
                new Cluster
                {
                    Id = id,
                    HealthCheck = new HealthCheckOptions
                    {
                        Passive = new PassiveHealthCheckOptions
                        {
                            Enabled = true,
                            Policy = "policy",
                            ReactivationPeriod = reactivationPeriod,
                        }
                    },
                    Metadata = metadata,
                },
                null);
            var clusterInfo = new ClusterInfo(id, new DestinationManager());
            clusterInfo.Config = clusterConfig;
            for (var i = 0; i < destinationCount; i++)
            {
                var destinationConfig = new DestinationConfig(new Destination { Address = $"https://localhost:1000{i}/{id}/", Health = $"https://localhost:2000{i}/{id}/" });
                var destinationId = $"destination{i}";
                clusterInfo.DestinationManager.GetOrCreateItem(destinationId, d =>
                {
                    d.Config = destinationConfig;
                });
            }

            return clusterInfo;
        }
    }
}

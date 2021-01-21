// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Utilities;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    public class ActiveHealthCheckMonitorTests
    {
        private const long Interval0 = 10000;
        private const long Interval1 = 20000;

        [Fact]
        public async Task CheckHealthAsync_ActiveHealthCheckIsEnabledForCluster_SendProbe()
        {
            var policy0 = new Mock<IActiveHealthCheckPolicy>();
            policy0.SetupGet(p => p.Name).Returns("policy0");
            var policy1 = new Mock<IActiveHealthCheckPolicy>();
            policy1.SetupGet(p => p.Name).Returns("policy1");
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var clusters = new List<ClusterInfo>();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), new Mock<ITimerFactory>().Object, GetLogger());

            var httpClient0 = GetHttpClient();
            var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object);
            clusters.Add(cluster0);
            var httpClient1 = GetHttpClient();
            var cluster1 = GetClusterInfo("cluster1", "policy0", false, httpClient1.Object);
            clusters.Add(cluster1);
            var httpClient2 = GetHttpClient();
            var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object);
            clusters.Add(cluster2);

            await monitor.CheckHealthAsync(clusters);

            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 1), ("https://localhost:20001/cluster0/api/health/", 1) });

            httpClient1.Verify(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Never);

            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) });
        }

        [Fact]
        public async Task ProbeCluster_ProbingTimerFired_SendProbesAndReceiveResponses()
        {
            var policy0 = new Mock<IActiveHealthCheckPolicy>();
            policy0.SetupGet(p => p.Name).Returns("policy0");
            var policy1 = new Mock<IActiveHealthCheckPolicy>();
            policy1.SetupGet(p => p.Name).Returns("policy1");
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            using var timerFactory = new TestTimerFactory();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), timerFactory, GetLogger());

            var httpClient0 = GetHttpClient();
            var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, TimeSpan.FromMilliseconds(Interval0));
            monitor.OnClusterAdded(cluster0);
            var httpClient2 = GetHttpClient();
            var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, TimeSpan.FromMilliseconds(Interval1));
            monitor.OnClusterAdded(cluster2);

            await monitor.CheckHealthAsync(new ClusterInfo[0]);

            timerFactory.FireAll();

            Assert.Equal(2, timerFactory.Count);
            timerFactory.VerifyTimer(0, Interval0);
            timerFactory.VerifyTimer(1, Interval1);
            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 1), ("https://localhost:20001/cluster0/api/health/", 1) }, policyCallTimes: 1);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);
        }

        [Fact]
        public async Task ProbeCluster_ClusterRemoved_StopSendingProbes()
        {
            var policy0 = new Mock<IActiveHealthCheckPolicy>();
            policy0.SetupGet(p => p.Name).Returns("policy0");
            var policy1 = new Mock<IActiveHealthCheckPolicy>();
            policy1.SetupGet(p => p.Name).Returns("policy1");
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            using var timerFactory = new TestTimerFactory();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), timerFactory, GetLogger());

            var httpClient0 = GetHttpClient();
            var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, interval: TimeSpan.FromMilliseconds(Interval0));
            monitor.OnClusterAdded(cluster0);
            var httpClient2 = GetHttpClient();
            var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, interval: TimeSpan.FromMilliseconds(Interval1));
            monitor.OnClusterAdded(cluster2);

            await monitor.CheckHealthAsync(new ClusterInfo[0]);

            timerFactory.FireAll();

            Assert.Equal(2, timerFactory.Count);
            timerFactory.VerifyTimer(0, Interval0);
            timerFactory.VerifyTimer(1, Interval1);
            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 1), ("https://localhost:20001/cluster0/api/health/", 1) }, policyCallTimes: 1);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);

            monitor.OnClusterRemoved(cluster2);

            timerFactory.FireTimer(0);

            timerFactory.AssertTimerDisposed(1);

            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 2), ("https://localhost:20001/cluster0/api/health/", 2) }, policyCallTimes: 2);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);
        }

        [Fact]
        public async Task ProbeCluster_ClusterAdded_StartSendingProbes()
        {
            var policy0 = new Mock<IActiveHealthCheckPolicy>();
            policy0.SetupGet(p => p.Name).Returns("policy0");
            var policy1 = new Mock<IActiveHealthCheckPolicy>();
            policy1.SetupGet(p => p.Name).Returns("policy1");
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var timerFactory = new TestTimerFactory();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), timerFactory, GetLogger());

            var httpClient0 = GetHttpClient();
            var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, interval: TimeSpan.FromMilliseconds(Interval0));
            monitor.OnClusterAdded(cluster0);

            await monitor.CheckHealthAsync(new ClusterInfo[0]);

            timerFactory.FireAll();

            Assert.Equal(1, timerFactory.Count);
            timerFactory.VerifyTimer(0, Interval0);
            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 1), ("https://localhost:20001/cluster0/api/health/", 1) }, policyCallTimes: 1);

            var httpClient2 = GetHttpClient();
            var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, interval: TimeSpan.FromMilliseconds(Interval1));
            monitor.OnClusterAdded(cluster2);

            timerFactory.FireAll();

            Assert.Equal(2, timerFactory.Count);
            timerFactory.VerifyTimer(0, Interval0);
            timerFactory.VerifyTimer(1, Interval1);
            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 2), ("https://localhost:20001/cluster0/api/health/", 2) }, policyCallTimes: 2);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);
        }

        [Fact]
        public async Task ProbeCluster_ClusterChanged_SendProbesToNewHealthEndpoint()
        {
            var policy0 = new Mock<IActiveHealthCheckPolicy>();
            policy0.SetupGet(p => p.Name).Returns("policy0");
            var policy1 = new Mock<IActiveHealthCheckPolicy>();
            policy1.SetupGet(p => p.Name).Returns("policy1");
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var timerFactory = new TestTimerFactory();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), timerFactory, GetLogger());

            var httpClient0 = GetHttpClient();
            var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, interval: TimeSpan.FromMilliseconds(Interval0));
            monitor.OnClusterAdded(cluster0);
            var httpClient2 = GetHttpClient();
            var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, interval: TimeSpan.FromMilliseconds(Interval1));
            monitor.OnClusterAdded(cluster2);

            await monitor.CheckHealthAsync(new ClusterInfo[0]);

            timerFactory.FireAll();

            Assert.Equal(2, timerFactory.Count);
            timerFactory.VerifyTimer(0, Interval0);
            timerFactory.VerifyTimer(1, Interval1);
            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 1), ("https://localhost:20001/cluster0/api/health/", 1) }, policyCallTimes: 1);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);

            foreach (var destination in cluster2.DestinationManager.Items)
            {
                var newDestinationConfig = new DestinationConfig(destination.Config.Address, null);
                cluster2.DestinationManager.GetOrCreateItem(destination.DestinationId, d =>
                {
                    d.Config = newDestinationConfig;
                });
            }

            monitor.OnClusterChanged(cluster2);

            timerFactory.FireAll();

            Assert.Equal(2, timerFactory.Count);
            timerFactory.VerifyTimer(0, Interval0);
            timerFactory.VerifyTimer(1, Interval1);
            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 2), ("https://localhost:20001/cluster0/api/health/", 2) }, policyCallTimes: 2);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:10000/cluster2/api/health/", 1), ("https://localhost:10001/cluster2/api/health/", 1) }, policyCallTimes: 2);
        }

        [Fact]
        public async Task ProbeCluster_ClusterChanged_StopSendingProbes()
        {
            var policy0 = new Mock<IActiveHealthCheckPolicy>();
            policy0.SetupGet(p => p.Name).Returns("policy0");
            var policy1 = new Mock<IActiveHealthCheckPolicy>();
            policy1.SetupGet(p => p.Name).Returns("policy1");
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var timerFactory = new TestTimerFactory();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), timerFactory, GetLogger());

            var httpClient0 = GetHttpClient();
            var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, interval: TimeSpan.FromMilliseconds(Interval0));
            monitor.OnClusterAdded(cluster0);
            var httpClient2 = GetHttpClient();
            var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, interval: TimeSpan.FromMilliseconds(Interval1));
            monitor.OnClusterAdded(cluster2);

            await monitor.CheckHealthAsync(new ClusterInfo[0]);

            timerFactory.FireAll();

            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 1), ("https://localhost:20001/cluster0/api/health/", 1) }, policyCallTimes: 1);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);

            var healthCheckConfig = new HealthCheckOptions
            {
                Passive = new PassiveHealthCheckOptions
                {
                    Enabled = true,
                    Policy = "passive0",
                },
                Active = new ActiveHealthCheckOptions
                {
                    Policy = cluster2.Config.Options.HealthCheck.Active.Policy,
                }
            };
            cluster2.Config = new ClusterConfig(new Cluster { Id = cluster2.ClusterId, HealthCheck = healthCheckConfig },
                cluster2.Config.HttpClient, default, default, null);

            monitor.OnClusterChanged(cluster2);

            timerFactory.FireTimer(0);

            timerFactory.AssertTimerDisposed(1);
            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 2), ("https://localhost:20001/cluster0/api/health/", 2) }, policyCallTimes: 2);
        }

        [Fact]
        public async Task ProbeCluster_UnsuccessfulResponseReceivedOrExceptionThrown_ReportItToPolicy()
        {
            var policy = new Mock<IActiveHealthCheckPolicy>();
            policy.SetupGet(p => p.Name).Returns("policy0");
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var clusters = new List<ClusterInfo>();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), new Mock<ITimerFactory>().Object, GetLogger());

            var httpClient = new Mock<HttpMessageInvoker>(() => new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));
            httpClient.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .Returns((HttpRequestMessage m, CancellationToken t) => GetResponse(m, t));
            var cluster = GetClusterInfo("cluster0", "policy0", true, httpClient.Object, destinationCount: 3);
            clusters.Add(cluster);

            await monitor.CheckHealthAsync(clusters);

            policy.Verify(
                p => p.ProbingCompleted(
                    cluster,
                    It.Is<IReadOnlyList<DestinationProbingResult>>(
                        r => r.Count == 3
                        && r.Single(i => i.Destination.DestinationId == "destination0").Response.StatusCode == HttpStatusCode.InternalServerError
                        && r.Single(i => i.Destination.DestinationId == "destination0").Exception == null
                        && r.Single(i => i.Destination.DestinationId == "destination1").Response == null
                        && r.Single(i => i.Destination.DestinationId == "destination1").Exception.GetType() == typeof(InvalidOperationException)
                        && r.Single(i => i.Destination.DestinationId == "destination2").Response.StatusCode == HttpStatusCode.OK
                        && r.Single(i => i.Destination.DestinationId == "destination2").Exception == null)),
                Times.Once);
            policy.Verify(p => p.Name);
            policy.VerifyNoOtherCalls();

            async Task<HttpResponseMessage> GetResponse(HttpRequestMessage m, CancellationToken t)
            {
                return await Task.Run(() =>
                {
                    switch (m.RequestUri.AbsoluteUri)
                    {
                        case "https://localhost:20000/cluster0/api/health/":
                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Version = m.Version };
                        case "https://localhost:20001/cluster0/api/health/":
                            throw new InvalidOperationException();
                        default:
                            return new HttpResponseMessage(HttpStatusCode.OK) { Version = m.Version };
                    }
                });
            }
        }

        [Fact]
        public async Task ForceCheckAll_SendingProbeToDestinationThrowsException_SkipItAndProceedToNextDestination()
        {
            var policy = new Mock<IActiveHealthCheckPolicy>();
            policy.SetupGet(p => p.Name).Returns("policy0");
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var clusters = new List<ClusterInfo>();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), new Mock<ITimerFactory>().Object, GetLogger());

            var httpClient = new Mock<HttpMessageInvoker>(() => new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));
            httpClient.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequestMessage m, CancellationToken t) => {
                    switch (m.RequestUri.AbsoluteUri)
                    {
                        case "https://localhost:20001/cluster0/api/health/":
                            throw new InvalidOperationException();
                        default:
                            return new HttpResponseMessage(HttpStatusCode.OK) { Version = m.Version };
                    }
                });
            var cluster = GetClusterInfo("cluster0", "policy0", true, httpClient.Object, destinationCount: 3);
            clusters.Add(cluster);

            await monitor.CheckHealthAsync(clusters);

            policy.Verify(
                p => p.ProbingCompleted(
                    cluster,
                    It.Is<IReadOnlyList<DestinationProbingResult>>(r => r.Count == 2 && r.All(i => i.Response.StatusCode == HttpStatusCode.OK && i.Exception == null))),
                Times.Once);
            policy.Verify(p => p.Name);
            policy.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ForceCheckAll_PolicyThrowsException_SkipItAndSetIsFullyInitializedFlag()
        {
            var policy = new Mock<IActiveHealthCheckPolicy>();
            policy.SetupGet(p => p.Name).Returns("policy0");
            policy.Setup(p => p.ProbingCompleted(It.IsAny<ClusterInfo>(), It.IsAny<IReadOnlyList<DestinationProbingResult>>())).Throws<InvalidOperationException>();
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var clusters = new List<ClusterInfo>();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), new Mock<ITimerFactory>().Object, GetLogger());

            var httpClient = GetHttpClient();
            var cluster = GetClusterInfo("cluster0", "policy0", true, httpClient.Object);
            clusters.Add(cluster);

            await monitor.CheckHealthAsync(clusters);

            policy.Verify(p => p.ProbingCompleted(It.IsAny<ClusterInfo>(), It.IsAny<IReadOnlyList<DestinationProbingResult>>()), Times.Once);
            policy.Verify(p => p.Name);
            policy.VerifyNoOtherCalls();
        }

        private static void VerifySentProbeAndResult(ClusterInfo cluster, Mock<HttpMessageInvoker> httpClient, Mock<IActiveHealthCheckPolicy> policy, (string RequestUri, int Times)[] probes, int policyCallTimes = 1)
        {
            foreach(var probe in probes)
            {
                httpClient.Verify(c => c.SendAsync(It.Is<HttpRequestMessage>(m => m.RequestUri.AbsoluteUri == probe.RequestUri), It.IsAny<CancellationToken>()), Times.Exactly(probe.Times));
            }
            httpClient.VerifyNoOtherCalls();
            policy.Verify(
                p => p.ProbingCompleted(
                    cluster,
                    It.Is<IReadOnlyList<DestinationProbingResult>>(r => cluster.DestinationManager.Items.All(d => r.Any(i => i.Destination == d && i.Response.StatusCode == HttpStatusCode.OK)))),
                Times.Exactly(policyCallTimes));
            policy.Verify(p => p.Name);
            policy.VerifyNoOtherCalls();
        }

        private ClusterInfo GetClusterInfo(string id, string policy, bool activeCheckEnabled, HttpMessageInvoker httpClient, TimeSpan? interval = null, TimeSpan? timeout = null, int destinationCount = 2)
        {
            var clusterConfig = new ClusterConfig(
                new Cluster
                {
                    Id = id,
                    HealthCheck = new HealthCheckOptions
                    {
                        Active = new ActiveHealthCheckOptions
                        {
                            Enabled = activeCheckEnabled,
                            Interval = interval,
                            Timeout = timeout,
                            Policy = policy,
                            Path = "/api/health/",
                        }
                    }
                },
                httpClient,
                default,
                default,
                null);
            var clusterInfo = new ClusterInfo(id, new DestinationManager());
            clusterInfo.Config = clusterConfig;
            for (var i = 0; i < destinationCount; i++)
            {
                var destinationConfig = new DestinationConfig($"https://localhost:1000{i}/{id}/", $"https://localhost:2000{i}/{id}/");
                var destinationId = $"destination{i}";
                clusterInfo.DestinationManager.GetOrCreateItem(destinationId, d =>
                {
                    d.Config = destinationConfig;
                });
            }
            clusterInfo.UpdateDynamicState();

            return clusterInfo;
        }

        private Mock<HttpMessageInvoker> GetHttpClient()
        {
            var httpClient = new Mock<HttpMessageInvoker>(() => new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));
            httpClient.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequestMessage m, CancellationToken c) => new HttpResponseMessage(HttpStatusCode.OK) { Version = m.Version });
            return httpClient;
        }

        private static ILogger<ActiveHealthCheckMonitor> GetLogger()
        {
            return new Mock<ILogger<ActiveHealthCheckMonitor>>().Object;
        }
    }
}

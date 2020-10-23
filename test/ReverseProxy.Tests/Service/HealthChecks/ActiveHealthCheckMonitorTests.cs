// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    public class ActiveHealthCheckMonitorTests
    {
        [Fact]
        public async Task ForceCheckAll_ActiveHealthCheckIsEnabledForCluster_SendProbe()
        {
            var policy0 = new Mock<IActiveHealthCheckPolicy>();
            policy0.SetupGet(p => p.Name).Returns("policy0");
            var policy1 = new Mock<IActiveHealthCheckPolicy>();
            policy1.SetupGet(p => p.Name).Returns("policy1");
            var proxyAppState = new ProxyAppState();
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), proxyAppState);

            var httpClient0 = GetHttpClient();
            var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object);
            monitor.OnClusterAdded(cluster0);
            var httpClient1 = GetHttpClient();
            var cluster1 = GetClusterInfo("cluster1", "policy0", false, httpClient1.Object);
            monitor.OnClusterAdded(cluster1);
            var httpClient2 = GetHttpClient();
            var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object);
            monitor.OnClusterAdded(cluster2);

            await monitor.CheckHealthAsync().ConfigureAwait(false);
            await proxyAppState.InitializationTask.ConfigureAwait(false);

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
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), new ProxyAppState());

            var httpClient0 = GetHttpClient();
            var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, interval: TimeSpan.FromSeconds(1));
            monitor.OnClusterAdded(cluster0);
            var httpClient2 = GetHttpClient();
            var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, interval: TimeSpan.FromSeconds(2));
            monitor.OnClusterAdded(cluster2);

            await monitor.CheckHealthAsync().ConfigureAwait(false);

            await Task.Delay(2500);

            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 3), ("https://localhost:20001/cluster0/api/health/", 3) }, policyCallTimes: 3);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 2), ("https://localhost:20001/cluster2/api/health/", 2) }, policyCallTimes: 2);
        }

        [Fact]
        public async Task ProbeCluster_ClusterRemoved_StopSendingProbes()
        {
            var policy0 = new Mock<IActiveHealthCheckPolicy>();
            policy0.SetupGet(p => p.Name).Returns("policy0");
            var policy1 = new Mock<IActiveHealthCheckPolicy>();
            policy1.SetupGet(p => p.Name).Returns("policy1");
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), new ProxyAppState());

            var httpClient0 = GetHttpClient();
            var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, interval: TimeSpan.FromSeconds(1));
            monitor.OnClusterAdded(cluster0);
            var httpClient2 = GetHttpClient();
            var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, interval: TimeSpan.FromSeconds(1));
            monitor.OnClusterAdded(cluster2);

            await monitor.CheckHealthAsync().ConfigureAwait(false);

            await Task.Delay(1500);

            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 2), ("https://localhost:20001/cluster0/api/health/", 2) }, policyCallTimes: 2);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 2), ("https://localhost:20001/cluster2/api/health/", 2) }, policyCallTimes: 2);

            monitor.OnClusterRemoved(cluster2);

            await Task.Delay(1200);

            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 3), ("https://localhost:20001/cluster0/api/health/", 3) }, policyCallTimes: 3);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 2), ("https://localhost:20001/cluster2/api/health/", 2) }, policyCallTimes: 2);
        }

        [Fact]
        public async Task ProbeCluster_ClusterAdded_StartSendingProbes()
        {
            var policy0 = new Mock<IActiveHealthCheckPolicy>();
            policy0.SetupGet(p => p.Name).Returns("policy0");
            var policy1 = new Mock<IActiveHealthCheckPolicy>();
            policy1.SetupGet(p => p.Name).Returns("policy1");
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), new ProxyAppState());

            var httpClient0 = GetHttpClient();
            var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, interval: TimeSpan.FromSeconds(1));
            monitor.OnClusterAdded(cluster0);

            await monitor.CheckHealthAsync().ConfigureAwait(false);

            await Task.Delay(1500);

            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 2), ("https://localhost:20001/cluster0/api/health/", 2) }, policyCallTimes: 2);

            var httpClient2 = GetHttpClient();
            var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, interval: TimeSpan.FromSeconds(1));
            monitor.OnClusterAdded(cluster2);

            await Task.Delay(1200);

            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 3), ("https://localhost:20001/cluster0/api/health/", 3) }, policyCallTimes: 3);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 1), ("https://localhost:20001/cluster2/api/health/", 1) }, policyCallTimes: 1);
        }

        [Fact]
        public async Task ProbeCluster_ClusterChanged_StopSendingProbes()
        {
            var policy0 = new Mock<IActiveHealthCheckPolicy>();
            policy0.SetupGet(p => p.Name).Returns("policy0");
            var policy1 = new Mock<IActiveHealthCheckPolicy>();
            policy1.SetupGet(p => p.Name).Returns("policy1");
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), new ProxyAppState());

            var httpClient0 = GetHttpClient();
            var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object, interval: TimeSpan.FromSeconds(1));
            monitor.OnClusterAdded(cluster0);
            var httpClient2 = GetHttpClient();
            var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object, interval: TimeSpan.FromSeconds(1));
            monitor.OnClusterAdded(cluster2);

            await monitor.CheckHealthAsync().ConfigureAwait(false);

            await Task.Delay(1500);

            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 2), ("https://localhost:20001/cluster0/api/health/", 2) }, policyCallTimes: 2);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:20000/cluster2/api/health/", 2), ("https://localhost:20001/cluster2/api/health/", 2) }, policyCallTimes: 2);

            foreach (var destination in cluster2.DestinationManager.Items.Value)
            {
                var newDestinationConfig = new DestinationConfig(destination.Config.Address, null);
                cluster2.DestinationManager.GetOrCreateItem(destination.DestinationId, d =>
                {
                    d.ConfigSignal.Value = newDestinationConfig;
                });
            }

            monitor.OnClusterChanged(cluster2);

            await Task.Delay(1200);

            VerifySentProbeAndResult(cluster0, httpClient0, policy0, new[] { ("https://localhost:20000/cluster0/api/health/", 3), ("https://localhost:20001/cluster0/api/health/", 3) }, policyCallTimes: 3);
            VerifySentProbeAndResult(cluster2, httpClient2, policy1, new[] { ("https://localhost:10000/cluster2/api/health/", 1), ("https://localhost:10001/cluster2/api/health/", 1) }, policyCallTimes: 3);
        }

        [Fact]
        public async Task ProbeCluster_UnsuccessfulResponseReceivedOrExceptionThrown_ReportItToPolicy()
        {
            var policy = new Mock<IActiveHealthCheckPolicy>();
            policy.SetupGet(p => p.Name).Returns("policy0");
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var proxyAppState = new ProxyAppState();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), proxyAppState);

            var httpClient = new Mock<HttpMessageInvoker>(() => new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));
            httpClient.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .Returns((HttpRequestMessage m, CancellationToken t) => GetResponse(m, t));
            var cluster = GetClusterInfo("cluster0", "policy0", true, httpClient.Object, destinationCount: 3);
            monitor.OnClusterAdded(cluster);

            await monitor.CheckHealthAsync().ConfigureAwait(false);
            Assert.True(proxyAppState.InitializationTask.IsCompleted);

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
            var proxyAppState = new ProxyAppState();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), proxyAppState);

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
            monitor.OnClusterAdded(cluster);

            await monitor.CheckHealthAsync().ConfigureAwait(false);
            Assert.True(proxyAppState.InitializationTask.IsCompleted);

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
            var proxyAppState = new ProxyAppState();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy.Object }, new DefaultProbingRequestFactory(), proxyAppState);

            var httpClient = GetHttpClient();
            var cluster = GetClusterInfo("cluster0", "policy0", true, httpClient.Object);
            monitor.OnClusterAdded(cluster);

            await monitor.CheckHealthAsync().ConfigureAwait(false);
            await proxyAppState.InitializationTask.ConfigureAwait(false);

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
                    It.Is<IReadOnlyList<DestinationProbingResult>>(r => cluster.DestinationManager.Items.Value.All(d => r.Any(i => i.Destination == d && i.Response.StatusCode == HttpStatusCode.OK)))),
                Times.Exactly(policyCallTimes));
            policy.Verify(p => p.Name);
            policy.VerifyNoOtherCalls();
        }

        private ClusterInfo GetClusterInfo(string id, string policy, bool activeCheckEnabled, HttpMessageInvoker httpClient, TimeSpan? interval = null, TimeSpan? timeout = null, int destinationCount = 2)
        {
            var clusterConfig = new ClusterConfig(
                new Cluster { Id = id },
                new ClusterConfig.ClusterHealthCheckOptions(default, new ClusterConfig.ClusterActiveHealthCheckOptions(activeCheckEnabled, interval, timeout, policy, "/api/health/")),
                default,
                default,
                httpClient,
                default,
                null);
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

        private Mock<HttpMessageInvoker> GetHttpClient()
        {
            var httpClient = new Mock<HttpMessageInvoker>(() => new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));
            httpClient.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequestMessage m, CancellationToken c) => new HttpResponseMessage(HttpStatusCode.OK) { Version = m.Version });
            return httpClient;
        }
    }
}

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
using Microsoft.ReverseProxy.Utilities;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    public class ActiveHealthCheckMonitorTests
    {
        [Fact]
        public async Task ForceCheckAll_ActiveHealthCheckIsEnabledForCluster_SendProbe()
        {
            var options = Options.Create(new ActiveHealthCheckMonitorOptions { DefaultInterval = TimeSpan.FromSeconds(60), DefaultTimeout = TimeSpan.FromSeconds(5) });
            var policy0 = new Mock<IActiveHealthCheckPolicy>();
            policy0.SetupGet(p => p.Name).Returns("policy0");
            var policy1 = new Mock<IActiveHealthCheckPolicy>();
            policy1.SetupGet(p => p.Name).Returns("policy1");
            var proxyAppState = new ProxyAppState();
            var monitor = new ActiveHealthCheckMonitor(options, new[] { policy0.Object, policy1.Object }, new DefaultProbingRequestFactory(), new UptimeClock(), proxyAppState);

            var httpClient0 = GetHttpClient();
            var cluster0 = GetClusterInfo("cluster0", "policy0", true, httpClient0.Object);
            monitor.OnClusterAdded(cluster0);
            var httpClient1 = GetHttpClient();
            var cluster1 = GetClusterInfo("cluster1", "policy0", false, httpClient1.Object);
            monitor.OnClusterAdded(cluster1);
            var httpClient2 = GetHttpClient();
            var cluster2 = GetClusterInfo("cluster2", "policy1", true, httpClient2.Object);
            monitor.OnClusterAdded(cluster2);

            _ = monitor.ForceCheckAll();
            await proxyAppState.WaitForFullInitialization().ConfigureAwait(false);

            httpClient0.Verify(c => c.SendAsync(It.Is<HttpRequestMessage>(m => m.RequestUri.AbsoluteUri == "https://localhost:20000/cluster0/api/health/"), It.IsAny<CancellationToken>()), Times.Once);
            httpClient0.Verify(c => c.SendAsync(It.Is<HttpRequestMessage>(m => m.RequestUri.AbsoluteUri == "https://localhost:20001/cluster0/api/health/"), It.IsAny<CancellationToken>()), Times.Once);
            httpClient0.VerifyNoOtherCalls();
            policy0.Verify(
                p => p.ProbingCompleted(
                    cluster0,
                    It.Is<IReadOnlyList<DestinationProbingResult>>(r => cluster0.DestinationManager.Items.Value.All(d => r.Any(i => i.Destination == d && i.Response.StatusCode == HttpStatusCode.OK)))),
                Times.Once);
            policy0.Verify(p => p.Name);
            policy0.VerifyNoOtherCalls();

            httpClient1.Verify(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Never);

            httpClient2.Verify(c => c.SendAsync(It.Is<HttpRequestMessage>(m => m.RequestUri.AbsoluteUri == "https://localhost:20000/cluster2/api/health/"), It.IsAny<CancellationToken>()), Times.Once);
            httpClient2.Verify(c => c.SendAsync(It.Is<HttpRequestMessage>(m => m.RequestUri.AbsoluteUri == "https://localhost:20001/cluster2/api/health/"), It.IsAny<CancellationToken>()), Times.Once);
            httpClient2.VerifyNoOtherCalls();
            policy0.Verify(
                p => p.ProbingCompleted(
                    cluster2,
                    It.Is<IReadOnlyList<DestinationProbingResult>>(r => cluster2.DestinationManager.Items.Value.All(d => r.Any(i => i.Destination == d && i.Response.StatusCode == HttpStatusCode.OK)))),
                Times.Once);
            policy1.Verify(p => p.Name);
            policy1.VerifyNoOtherCalls();
        }

        private ClusterInfo GetClusterInfo(string id, string policy, bool activeCheckEnabled, HttpMessageInvoker httpClient)
        {
            var clusterConfig = new ClusterConfig(
                new Cluster { Id = id },
                new ClusterConfig.ClusterHealthCheckOptions(default, new ClusterConfig.ClusterActiveHealthCheckOptions(activeCheckEnabled, null, null, policy, "api/health/")),
                default,
                default,
                httpClient,
                default,
                null);
            var clusterInfo = new ClusterInfo(id, new DestinationManager(null));
            clusterInfo.ConfigSignal.Value = clusterConfig;
            for (var i = 0; i < 2; i++)
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

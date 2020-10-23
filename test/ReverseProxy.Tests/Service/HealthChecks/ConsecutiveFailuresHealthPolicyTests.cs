// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    public class ConsecutiveFailuresHealthPolicyTests
    {
        [Fact]
        public void ProbingCompleted_FailureThresholdExceeded_MarkDestinationUnhealthy()
        {
            var options = Options.Create(new ConsecutiveFailuresHealthPolicyOptions { DefaultThreshold = 2 });
            var policy = new ConsecutiveFailuresHealthPolicy(options, new Mock<ILogger<ConsecutiveFailuresHealthPolicy>>().Object);
            var cluster0 = GetClusterInfo("cluster0", destinationCount: 2);
            var cluster1 = GetClusterInfo("cluster0", destinationCount: 2, failureThreshold: 3);

            var probingResults0 = new[] {
                new DestinationProbingResult(cluster0.DestinationManager.Items.Value[0], new HttpResponseMessage(HttpStatusCode.InternalServerError), null),
                new DestinationProbingResult(cluster0.DestinationManager.Items.Value[1], new HttpResponseMessage(HttpStatusCode.OK), null)
            };
            var probingResults1 = new[] {
                new DestinationProbingResult(cluster1.DestinationManager.Items.Value[0], new HttpResponseMessage(HttpStatusCode.OK), null),
                new DestinationProbingResult(cluster1.DestinationManager.Items.Value[1], null, new InvalidOperationException())
            };

            Assert.Equal(HealthCheckConstants.ActivePolicy.ConsecutiveFailures, policy.Name);

            // Initial state
            Assert.All(cluster0.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Unknown, d.DynamicState.Health.Active));
            Assert.All(cluster1.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Unknown, d.DynamicState.Health.Active));

            // First probing attempt
            policy.ProbingCompleted(cluster0, probingResults0);
            Assert.All(cluster0.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Healthy, d.DynamicState.Health.Active));
            policy.ProbingCompleted(cluster1, probingResults1);
            Assert.All(cluster1.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Healthy, d.DynamicState.Health.Active));

            // Second probing attempt
            policy.ProbingCompleted(cluster0, probingResults0);
            Assert.Equal(DestinationHealth.Unhealthy, cluster0.DestinationManager.Items.Value[0].DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Healthy, cluster0.DestinationManager.Items.Value[1].DynamicState.Health.Active);
            policy.ProbingCompleted(cluster1, probingResults1);
            Assert.All(cluster1.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Healthy, d.DynamicState.Health.Active));

            // Third probing attempt
            policy.ProbingCompleted(cluster0, probingResults0);
            Assert.Equal(DestinationHealth.Unhealthy, cluster0.DestinationManager.Items.Value[0].DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Healthy, cluster0.DestinationManager.Items.Value[1].DynamicState.Health.Active);
            policy.ProbingCompleted(cluster1, probingResults1);
            Assert.Equal(DestinationHealth.Healthy, cluster1.DestinationManager.Items.Value[0].DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, cluster1.DestinationManager.Items.Value[1].DynamicState.Health.Active);

            Assert.All(cluster0.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Unknown, d.DynamicState.Health.Passive));
            Assert.All(cluster1.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Unknown, d.DynamicState.Health.Passive));
        }

        [Fact]
        public void ProbingCompleted_SuccessfulResponse_MarkDestinationHealthy()
        {
            var options = Options.Create(new ConsecutiveFailuresHealthPolicyOptions { DefaultThreshold = 2 });
            var policy = new ConsecutiveFailuresHealthPolicy(options, new Mock<ILogger<ConsecutiveFailuresHealthPolicy>>().Object);
            var cluster = GetClusterInfo("cluster0", destinationCount: 2);

            var probingResults = new[] {
                new DestinationProbingResult(cluster.DestinationManager.Items.Value[0], new HttpResponseMessage(HttpStatusCode.InternalServerError), null),
                new DestinationProbingResult(cluster.DestinationManager.Items.Value[1], new HttpResponseMessage(HttpStatusCode.OK), null)
            };

            for (var i = 0; i < 2; i++)
            {
                policy.ProbingCompleted(cluster, probingResults);
            }

            Assert.Equal(DestinationHealth.Unhealthy, cluster.DestinationManager.Items.Value[0].DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items.Value[1].DynamicState.Health.Active);

            policy.ProbingCompleted(cluster, new[] { new DestinationProbingResult(cluster.DestinationManager.Items.Value[0], new HttpResponseMessage(HttpStatusCode.OK), null) });

            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items.Value[0].DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items.Value[1].DynamicState.Health.Active);

            Assert.All(cluster.DestinationManager.Items.Value, d => Assert.Equal(DestinationHealth.Unknown, d.DynamicState.Health.Passive));
        }

        [Fact]
        public void ProbingCompleted_EmptyProbingResultList_DoNothing()
        {
            var options = Options.Create(new ConsecutiveFailuresHealthPolicyOptions { DefaultThreshold = 2 });
            var policy = new ConsecutiveFailuresHealthPolicy(options, new Mock<ILogger<ConsecutiveFailuresHealthPolicy>>().Object);
            var cluster = GetClusterInfo("cluster0", destinationCount: 2);

            var probingResults = new[] {
                new DestinationProbingResult(cluster.DestinationManager.Items.Value[0], new HttpResponseMessage(HttpStatusCode.InternalServerError), null),
                new DestinationProbingResult(cluster.DestinationManager.Items.Value[1], new HttpResponseMessage(HttpStatusCode.OK), null)
            };

            for (var i = 0; i < 2; i++)
            {
                policy.ProbingCompleted(cluster, probingResults);
            }

            Assert.Equal(DestinationHealth.Unhealthy, cluster.DestinationManager.Items.Value[0].DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items.Value[1].DynamicState.Health.Active);

            policy.ProbingCompleted(cluster, new DestinationProbingResult[0]);

            Assert.Equal(DestinationHealth.Unhealthy, cluster.DestinationManager.Items.Value[0].DynamicState.Health.Active);
            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items.Value[1].DynamicState.Health.Active);
        }

        private ClusterInfo GetClusterInfo(string id, int destinationCount, int? failureThreshold = null)
        {
            var metadata = failureThreshold != null
                ? new Dictionary<string, string> { { ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName, failureThreshold.ToString() } }
                : null;
            var clusterConfig = new ClusterConfig(
                new Cluster { Id = id },
                new ClusterConfig.ClusterHealthCheckOptions(default, new ClusterConfig.ClusterActiveHealthCheckOptions(true, null, null, "policy", "/api/health/")),
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
    }
}

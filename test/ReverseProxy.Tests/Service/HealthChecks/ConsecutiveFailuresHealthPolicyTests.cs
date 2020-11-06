// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Xunit;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    public class ConsecutiveFailuresHealthPolicyTests
    {
        [Fact]
        public void ProbingCompleted_FailureThresholdExceeded_MarkDestinationUnhealthy()
        {
            var options = Options.Create(new ConsecutiveFailuresHealthPolicyOptions { DefaultThreshold = 2 });
            var policy = new ConsecutiveFailuresHealthPolicy(options, new DestinationHealthUpdaterStub());
            var cluster0 = GetClusterInfo("cluster0", destinationCount: 2);
            var cluster1 = GetClusterInfo("cluster0", destinationCount: 2, failureThreshold: 3);

            var probingResults0 = new[] {
                new DestinationProbingResult(cluster0.DestinationManager.Items[0], new HttpResponseMessage(HttpStatusCode.InternalServerError), null),
                new DestinationProbingResult(cluster0.DestinationManager.Items[1], new HttpResponseMessage(HttpStatusCode.OK), null)
            };
            var probingResults1 = new[] {
                new DestinationProbingResult(cluster1.DestinationManager.Items[0], new HttpResponseMessage(HttpStatusCode.OK), null),
                new DestinationProbingResult(cluster1.DestinationManager.Items[1], null, new InvalidOperationException())
            };

            Assert.Equal(HealthCheckConstants.ActivePolicy.ConsecutiveFailures, policy.Name);

            // Initial state
            Assert.All(cluster0.DestinationManager.Items, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Active));
            Assert.All(cluster1.DestinationManager.Items, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Active));

            // First probing attempt
            policy.ProbingCompleted(cluster0, probingResults0);
            Assert.All(cluster0.DestinationManager.Items, d => Assert.Equal(DestinationHealth.Healthy, d.Health.Active));
            policy.ProbingCompleted(cluster1, probingResults1);
            Assert.All(cluster1.DestinationManager.Items, d => Assert.Equal(DestinationHealth.Healthy, d.Health.Active));

            // Second probing attempt
            policy.ProbingCompleted(cluster0, probingResults0);
            Assert.Equal(DestinationHealth.Unhealthy, cluster0.DestinationManager.Items[0].Health.Active);
            Assert.Equal(DestinationHealth.Healthy, cluster0.DestinationManager.Items[1].Health.Active);
            policy.ProbingCompleted(cluster1, probingResults1);
            Assert.All(cluster1.DestinationManager.Items, d => Assert.Equal(DestinationHealth.Healthy, d.Health.Active));

            // Third probing attempt
            policy.ProbingCompleted(cluster0, probingResults0);
            Assert.Equal(DestinationHealth.Unhealthy, cluster0.DestinationManager.Items[0].Health.Active);
            Assert.Equal(DestinationHealth.Healthy, cluster0.DestinationManager.Items[1].Health.Active);
            policy.ProbingCompleted(cluster1, probingResults1);
            Assert.Equal(DestinationHealth.Healthy, cluster1.DestinationManager.Items[0].Health.Active);
            Assert.Equal(DestinationHealth.Unhealthy, cluster1.DestinationManager.Items[1].Health.Active);

            Assert.All(cluster0.DestinationManager.Items, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Passive));
            Assert.All(cluster1.DestinationManager.Items, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Passive));
        }

        [Fact]
        public void ProbingCompleted_SuccessfulResponse_MarkDestinationHealthy()
        {
            var options = Options.Create(new ConsecutiveFailuresHealthPolicyOptions { DefaultThreshold = 2 });
            var policy = new ConsecutiveFailuresHealthPolicy(options, new DestinationHealthUpdaterStub());
            var cluster = GetClusterInfo("cluster0", destinationCount: 2);

            var probingResults = new[] {
                new DestinationProbingResult(cluster.DestinationManager.Items[0], new HttpResponseMessage(HttpStatusCode.InternalServerError), null),
                new DestinationProbingResult(cluster.DestinationManager.Items[1], new HttpResponseMessage(HttpStatusCode.OK), null)
            };

            for (var i = 0; i < 2; i++)
            {
                policy.ProbingCompleted(cluster, probingResults);
            }

            Assert.Equal(DestinationHealth.Unhealthy, cluster.DestinationManager.Items[0].Health.Active);
            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items[1].Health.Active);

            policy.ProbingCompleted(cluster, new[] { new DestinationProbingResult(cluster.DestinationManager.Items[0], new HttpResponseMessage(HttpStatusCode.OK), null) });

            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items[0].Health.Active);
            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items[1].Health.Active);

            Assert.All(cluster.DestinationManager.Items, d => Assert.Equal(DestinationHealth.Unknown, d.Health.Passive));
        }

        [Fact]
        public void ProbingCompleted_EmptyProbingResultList_DoNothing()
        {
            var options = Options.Create(new ConsecutiveFailuresHealthPolicyOptions { DefaultThreshold = 2 });
            var policy = new ConsecutiveFailuresHealthPolicy(options, new DestinationHealthUpdaterStub());
            var cluster = GetClusterInfo("cluster0", destinationCount: 2);

            var probingResults = new[] {
                new DestinationProbingResult(cluster.DestinationManager.Items[0], new HttpResponseMessage(HttpStatusCode.InternalServerError), null),
                new DestinationProbingResult(cluster.DestinationManager.Items[1], new HttpResponseMessage(HttpStatusCode.OK), null)
            };

            for (var i = 0; i < 2; i++)
            {
                policy.ProbingCompleted(cluster, probingResults);
            }

            Assert.Equal(DestinationHealth.Unhealthy, cluster.DestinationManager.Items[0].Health.Active);
            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items[1].Health.Active);

            policy.ProbingCompleted(cluster, new DestinationProbingResult[0]);

            Assert.Equal(DestinationHealth.Unhealthy, cluster.DestinationManager.Items[0].Health.Active);
            Assert.Equal(DestinationHealth.Healthy, cluster.DestinationManager.Items[1].Health.Active);
        }

        private ClusterInfo GetClusterInfo(string id, int destinationCount, int? failureThreshold = null)
        {
            var metadata = failureThreshold != null
                ? new Dictionary<string, string> { { ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName, failureThreshold.ToString() } }
                : null;
            var clusterConfig = new ClusterConfig(
                new Cluster { Id = id },
                new ClusterHealthCheckOptions(default, new ClusterActiveHealthCheckOptions(true, null, null, "policy", "/api/health/")),
                default,
                default,
                null,
                default,
                default,
                metadata);
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

            return clusterInfo;
        }

        private class DestinationHealthUpdaterStub : IDestinationHealthUpdater
        {
            public void SetActive(ClusterInfo cluster, IEnumerable<(DestinationInfo Destination, DestinationHealth NewHealth)> newHealths)
            {
                foreach(var (destination, newHealth) in newHealths)
                {
                    destination.Health.Active = newHealth;
                }

                cluster.UpdateDynamicState();
            }

            public Task SetPassiveAsync(ClusterInfo cluster, DestinationInfo destination, DestinationHealth newHealth, TimeSpan reactivationPeriod)
            {
                throw new NotImplementedException();
            }
        }
    }
}

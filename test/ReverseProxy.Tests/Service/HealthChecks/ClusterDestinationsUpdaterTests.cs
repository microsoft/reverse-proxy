// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    public class ClusterDestinationsUpdaterTests
    {
        [Fact]
        public void UpdateAllDestinations_UseDestinationsCollectionAsSource()
        {
            var cluster = GetCluster("policy1");
            var destination0 = cluster.Destinations.GetOrAdd("d0", id => new DestinationState(id));
            var destination1 = cluster.Destinations.GetOrAdd("d1", id => new DestinationState(id));
            var destination2 = cluster.Destinations.GetOrAdd("d2", id => new DestinationState(id));
            var expectedAll = new[] { destination0, destination1, destination2 };
            var expectedAvailable = new[] { destination0, destination2 };
            var policy0 = new StubPolicy("policy0", destination1);
            var policy1 = new StubPolicy("policy1", destination1);
            var updater = new ClusterDestinationsUpdater(new[] { policy0, policy1 });

            updater.UpdateAllDestinations(cluster);

            Assert.Equal(cluster.DestinationsState.AllDestinations, expectedAll);
            Assert.Equal(cluster.DestinationsState.AvailableDestinations, expectedAvailable);

            Assert.False(policy0.IsCalled);
            Assert.Null(policy0.TakenDestinations);
            Assert.True(policy1.IsCalled);
            Assert.Equal(policy1.TakenDestinations, expectedAll);
        }

        private static ClusterState GetCluster(string policyName)
        {
            var cluster = new ClusterState("cluster1")
            {
                Model = new ClusterModel(
                    new ClusterConfig
                    {
                        ClusterId = "cluster1",
                        HealthCheck = new HealthCheckConfig { AvailableDestinationsPolicy = policyName }
                    },
                httpClient: new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object))
            };

            return cluster;
        }

        private class StubPolicy : IAvailableDestinationsPolicy
        {
            private readonly DestinationState _skipDestination;

            public bool IsCalled { get; private set; }

            public IReadOnlyList<DestinationState> TakenDestinations { get; private set; }

            public StubPolicy(string name, DestinationState skipDestination)
            {
                Name = name;
                _skipDestination = skipDestination;
            }

            public string Name { get; }

            public IReadOnlyList<DestinationState> GetAvailalableDestinations(ClusterConfig config, IReadOnlyList<DestinationState> allDestinations)
            {
                IsCalled = true;
                TakenDestinations = allDestinations;
                return allDestinations.Where(p => p != _skipDestination).ToArray();
            }
        }
    }
}

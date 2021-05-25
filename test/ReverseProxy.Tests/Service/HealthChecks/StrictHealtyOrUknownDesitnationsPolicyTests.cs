// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    public class StrictHealtyOrUknownDesitnationsPolicyTests
    {
        [Fact]
        public void GetAvailableDestinations_HealthChecksEnabled_FilterOutUnhealthy()
        {
            var cluster = new ClusterConfig()
            {
                ClusterId = "cluster1",
                HealthCheck = new HealthCheckConfig
                {
                    Active = new ActiveHealthCheckConfig { Enabled = true },
                    Passive = new PassiveHealthCheckConfig { Enabled = true }
                }
            };

            var allDestinations = new[]
            {
                new DestinationState("d1") { Health = { Active = DestinationHealth.Healthy } },
                new DestinationState("d2") { Health = { Active = DestinationHealth.Unhealthy } },
                new DestinationState("d3") { Health = { Active = DestinationHealth.Unhealthy, Passive = DestinationHealth.Healthy } },
                new DestinationState("d4") { Health = { Passive = DestinationHealth.Unhealthy } },
                new DestinationState("d5") { Health = { Passive = DestinationHealth.Healthy } },
                new DestinationState("d6") { Health = { Active = DestinationHealth.Healthy, Passive = DestinationHealth.Unhealthy } },
                new DestinationState("d7") { Health = { Active = DestinationHealth.Unhealthy, Passive = DestinationHealth.Unhealthy } },
                new DestinationState("d8")
            };
            var policy = new StrictHealthyAndUnknownDestinationsPolicy();

            var availableDestinations = policy.GetAvailalableDestinations(cluster, allDestinations);

            Assert.Equal(3, availableDestinations.Count);
            Assert.Same(allDestinations[0], availableDestinations[0]);
            Assert.Same(allDestinations[4], availableDestinations[1]);
            Assert.Same(allDestinations[7], availableDestinations[2]);
        }
    }
}

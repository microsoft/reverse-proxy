// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    public class BaseAvailableDestinationsPolicyTests
    {
        [Theory]
        [MemberData(nameof(GetDisabledHealthChecksCases))]
        public void GetAvailableDestinations_HealthChecksDisabled_ReturnAll(HealthCheckConfig config)
        {
            var cluster = new ClusterConfig() { ClusterId = "cluster1", HealthCheck = config };
            var allDestinations = new[]
            {
                new DestinationState("d1") { Health = { Active = DestinationHealth.Healthy } },
                new DestinationState("d2") { Health = { Active = DestinationHealth.Unhealthy, Passive = DestinationHealth.Healthy } },
                new DestinationState("d3") { Health = { Passive = DestinationHealth.Healthy } },
                new DestinationState("d4"),
                new DestinationState("d5") { Health = { Active = DestinationHealth.Healthy, Passive = DestinationHealth.Unhealthy } },
                new DestinationState("d6") { Health = { Active = DestinationHealth.Unhealthy, Passive = DestinationHealth.Unhealthy } }
            };
            var policy = new StubDestinationPolicy();

            var availableDestinations = policy.GetAvailalableDestinations(cluster, allDestinations);

            Assert.Equal(7, availableDestinations.Count);
            Assert.Same(allDestinations[0], availableDestinations[0]);
            Assert.Same(allDestinations[1], availableDestinations[1]);
            Assert.Same(allDestinations[2], availableDestinations[2]);
            Assert.Same(allDestinations[3], availableDestinations[3]);
            Assert.Same(allDestinations[4], availableDestinations[4]);
            Assert.Same(allDestinations[5], availableDestinations[5]);
            Assert.Same(allDestinations[6], availableDestinations[6]);
        }

        [Fact]
        public void GetAvailableDestinations_HealthChecksEnabled_Filter()
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
                new DestinationState("d2") { Health = { Active = DestinationHealth.Unhealthy, Passive = DestinationHealth.Healthy } },
                new DestinationState("d3") { Health = { Passive = DestinationHealth.Healthy } },
                new DestinationState("d4"),
                new DestinationState("d5") { Health = { Active = DestinationHealth.Healthy, Passive = DestinationHealth.Unhealthy } },
                new DestinationState("d6") { Health = { Active = DestinationHealth.Unhealthy, Passive = DestinationHealth.Unhealthy } }
            };
            var policy = new StubDestinationPolicy();

            var availableDestinations = policy.GetAvailalableDestinations(cluster, allDestinations);

            Assert.Equal(4, availableDestinations.Count);
            Assert.Same(allDestinations[0], availableDestinations[0]);
            Assert.Same(allDestinations[1], availableDestinations[1]);
            Assert.Same(allDestinations[2], availableDestinations[2]);
            Assert.Same(allDestinations[4], availableDestinations[3]);
        }

        [Theory]
        [InlineData(true, DestinationHealth.Healthy, false, DestinationHealth.Healthy)]
        [InlineData(false, DestinationHealth.Healthy, true, DestinationHealth.Healthy)]
        [InlineData(true, DestinationHealth.Unhealthy, false, DestinationHealth.Unhealthy)]
        [InlineData(false, DestinationHealth.Unhealthy, true, DestinationHealth.Unhealthy)]
        public void GetAvailableDestinations_OneHealthCheckDisabled_UseUnknownState(bool activeEnabled, DestinationHealth active, bool passiveEnabled, DestinationHealth passive)
        {
            var cluster = new ClusterConfig()
            {
                ClusterId = "cluster1",
                HealthCheck = new HealthCheckConfig
                {
                    Active = new ActiveHealthCheckConfig { Enabled = activeEnabled },
                    Passive = new PassiveHealthCheckConfig { Enabled = passiveEnabled }
                }
            };

            var policy = new StubDestinationPolicy();

            policy.GetAvailalableDestinations(cluster, new[] { new DestinationState("d0") { Health = { Active = active, Passive = passive } } });

            Assert.Equal(activeEnabled ? DestinationHealth.Healthy : DestinationHealth.Unknown, policy.LastActiveState);
            Assert.Equal(passiveEnabled ? DestinationHealth.Healthy : DestinationHealth.Unknown, policy.LastPassiveState);
        }

        private class StubDestinationPolicy : BaseAvailableDestinationsPolicy
        {
            public DestinationHealth LastActiveState { get; private set; }

            public DestinationHealth LastPassiveState { get; private set; }

            public override string Name => "Stub";

            protected override bool IsDestinationAvailable(DestinationState destination, DestinationHealth activeHealth, DestinationHealth passiveHealth)
            {
                LastActiveState = activeHealth;
                LastPassiveState = passiveHealth;
                return activeHealth == DestinationHealth.Healthy || passiveHealth == DestinationHealth.Healthy;
            }
        }

        public static IEnumerable<object[]> GetDisabledHealthChecksCases()
        {
            yield return new[] { new HealthCheckConfig() };
            yield return new[] { (object)null };
        }

        private static void EnableHealthChecks(ClusterState cluster)
        {
            // Pretend that health checks are enabled so that destination health states are honored
            cluster.Model = new ClusterModel(
                new ClusterConfig
                {
                    HealthCheck = new HealthCheckConfig
                    {
                        Passive = new PassiveHealthCheckConfig
                        {
                            Enabled = true,
                            Policy = "FailureRate",
                            ReactivationPeriod = TimeSpan.FromMinutes(5),
                        },
                        Active = new ActiveHealthCheckConfig
                        {
                            Enabled = true,
                            Interval = TimeSpan.FromSeconds(5),
                            Timeout = TimeSpan.FromSeconds(30),
                            Policy = "Any5xxResponse",
                            Path = "/",
                        }
                    }
                },
                httpClient: new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));
        }
    }
}

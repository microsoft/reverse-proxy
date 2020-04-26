// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;
using ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Proxy.Tests
{
    public class LoadBalancerTests : TestAutoMockBase
    {
        public LoadBalancerTests()
        {
            RandomFactory = new TestRandomFactory() { Instance = RandomInstance };
            Provide<IRandomFactory>(RandomFactory);
        }

        internal TestRandom RandomInstance { get; set; } = new TestRandom();

        internal TestRandomFactory RandomFactory { get; set; }

        [Fact]
        public void PickEndpoint_WithoutEndpoints_Null()
        {
            var loadBalancer = Create<LoadBalancer>();
            var endpoints = new EndpointInfo[0];
            var options = new BackendConfig.BackendLoadBalancingOptions((LoadBalancingMode)(-1));

            var result = loadBalancer.PickEndpoint(endpoints, in options);

            Assert.Null(result);
        }

        [Fact]
        public void PickEndpoint_SingleEndpoints_ShortCircuit()
        {
            var loadBalancer = Create<LoadBalancer>();
            var endpoints = new[]
            {
                new EndpointInfo("ep1"),
            };
            var options = new BackendConfig.BackendLoadBalancingOptions((LoadBalancingMode)(-1));

            var result = loadBalancer.PickEndpoint(endpoints, in options);

            Assert.Equal(result, endpoints[0]);
        }

        [Fact]
        public void PickEndpoint_UnsupportedMode_Throws()
        {
            var loadBalancer = Create<LoadBalancer>();
            var endpoints = new[]
            {
                new EndpointInfo("ep1"),
                new EndpointInfo("ep2"),
            };
            var options = new BackendConfig.BackendLoadBalancingOptions((LoadBalancingMode)(-1));

            Action action = () => loadBalancer.PickEndpoint(endpoints, in options);

            // Assert
            var ex = Assert.Throws<NotSupportedException>(action);
            Assert.Equal("Load balancing mode '-1' is not supported.", ex.Message);
        }

        [Fact]
        public void PickEndpoint_FirstWithEndpoints_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var endpoints = new[]
            {
                new EndpointInfo("ep1"),
                new EndpointInfo("ep2"),
            };
            var options = new BackendConfig.BackendLoadBalancingOptions(LoadBalancingMode.First);

            var result = loadBalancer.PickEndpoint(endpoints, in options);

            Assert.Same(endpoints[0], result);
        }

        [Fact]
        public void PickEndpoint_Random_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var endpoints = new[]
            {
                new EndpointInfo("ep1"),
                new EndpointInfo("ep2"),
            };
            RandomInstance.Sequence = new[] { 1 };
            var options = new BackendConfig.BackendLoadBalancingOptions(LoadBalancingMode.Random);

            var result = loadBalancer.PickEndpoint(endpoints, in options);

            Assert.Same(result, endpoints[1]);
        }

        [Fact]
        public void PickEndpoint_PowerOfTwoChoices_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var endpoints = new[]
            {
                new EndpointInfo("ep1"),
                new EndpointInfo("ep2"),
            };
            endpoints[0].ConcurrencyCounter.Increment();
            RandomInstance.Sequence = new[] { 1, 0 };
            var options = new BackendConfig.BackendLoadBalancingOptions(LoadBalancingMode.PowerOfTwoChoices);

            var result = loadBalancer.PickEndpoint(endpoints, in options);

            Assert.Same(result, endpoints[1]);
        }

        [Fact]
        public void PickEndpoint_LeastRequests_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var endpoints = new[]
            {
                new EndpointInfo("ep1"),
                new EndpointInfo("ep2"),
            };
            endpoints[0].ConcurrencyCounter.Increment();
            var options = new BackendConfig.BackendLoadBalancingOptions(LoadBalancingMode.LeastRequests);

            var result = loadBalancer.PickEndpoint(endpoints, in options);

            Assert.Same(result, endpoints[1]);
        }

        [Fact]
        public void PickEndpoint_RoundRobin_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var endpoints = new[]
            {
                new EndpointInfo("ep1"),
                new EndpointInfo("ep2"),
            };
            endpoints[0].ConcurrencyCounter.Increment();
            var options = new BackendConfig.BackendLoadBalancingOptions(LoadBalancingMode.RoundRobin);

            var result0 = loadBalancer.PickEndpoint(endpoints, in options);
            var result1 = loadBalancer.PickEndpoint(endpoints, in options);
            var result2 = loadBalancer.PickEndpoint(endpoints, in options);
            var result3 = loadBalancer.PickEndpoint(endpoints, in options);

            Assert.Same(result0, endpoints[0]);
            Assert.Same(result1, endpoints[1]);
            Assert.Same(result2, endpoints[0]);
            Assert.Same(result3, endpoints[1]);
        }

        [Fact]
        public void PickEndpoint_Callback_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var endpoints = new[]
            {
                new EndpointInfo("1"),
                new EndpointInfo("2"),
                new EndpointInfo("3")
            };
            var options = new BackendConfig.BackendLoadBalancingOptions(LoadBalancingMode.Callback,
                (availableEndpoints, _) => availableEndpoints.FirstOrDefault(x => x.EndpointId == "3"));

            // choose third
            var next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.NotNull(next);
            Assert.Equal("3", next.EndpointId);
        }

        [Fact]
        public void PickEndpoint_DeficitRoundRobin_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var first = new EndpointInfo("1");
            var second = new EndpointInfo("2");
            var third = new EndpointInfo("3");
            var endpoints = new[]
            {
                first,
                second,
                third
            };

            var quanta = new Dictionary<EndpointInfo, int>
            {
                [first] = 1,
                [second] = 2,
                [third] = 3
            };
            var options = new BackendConfig.BackendLoadBalancingOptions(LoadBalancingMode.DeficitRoundRobin,
                deficitRoundRobinQuanta: quanta);

            // choose first X 1
            var next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.Equal(first, next);

            // choose second X 2
            next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.Equal(second, next);
            next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.Equal(second, next);

            // choose third X 3
            next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.Equal(third, next);
            next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.Equal(third, next);
            next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.Equal(third, next);

            // choose first X 1
            next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.Equal(first, next);

            // choose second X 2
            next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.Equal(second, next);
            next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.Equal(second, next);

            // choose third X 3
            next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.Equal(third, next);
            next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.Equal(third, next);
            next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.Equal(third, next);
        }

        [Fact]
        public void PickEndpoint_FailOver_FailsOver()
        {
            var loadBalancer = Create<LoadBalancer>();
            var first = new EndpointInfo("1");
            var second = new EndpointInfo("2");
            var third = new EndpointInfo("3");
            var endpoints = new[]
            {
                first,
                second,
                third
            };

            var options = new BackendConfig.BackendLoadBalancingOptions(LoadBalancingMode.FailOver,
                failOverPreferredEndpoint: () => first,
                failOverIsAvailablePredicate: _ => false,
                failOverFallBackLoadBalancingStrategy: () => new RandomLoadBalancingStrategy(new RandomFactory()));

            // don't choose first
            var next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.NotNull(next);
            Assert.NotEqual(first, next);
        }

        [Fact]
        public void PickEndpoint_FailOver_DoesntFailOver()
        {
            var loadBalancer = Create<LoadBalancer>();
            var first = new EndpointInfo("1");
            var second = new EndpointInfo("2");
            var third = new EndpointInfo("3");
            var endpoints = new[]
            {
                first,
                second,
                third
            };

            var options = new BackendConfig.BackendLoadBalancingOptions(LoadBalancingMode.FailOver,
                failOverPreferredEndpoint: () => first,
                failOverIsAvailablePredicate: _ => true,
                failOverFallBackLoadBalancingStrategy: () => new RandomLoadBalancingStrategy(new RandomFactory()));

            // choose first
            var next = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
            Assert.NotNull(next);
            Assert.Equal(first, next);
        }

        [Fact]
        public void PickEndpoint_TrafficAllocation_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var first = new EndpointInfo("1");
            var second = new EndpointInfo("2");
            var third = new EndpointInfo("3");
            var endpoints = new[]
            {
                first,
                second,
                third
            };

            var variation = .10M;
            var options = new BackendConfig.BackendLoadBalancingOptions(LoadBalancingMode.TrafficAllocation,
                trafficAllocationSelector: x => x.Where(item => item == second),
                trafficAllocationVariation: variation,
                trafficAllocationBackingLoadBalancingStrategy: () => new RandomLoadBalancingStrategy(new RandomFactory()));

            var selections = 0;
            const int Iterations = 100 * 1000;
            for (var i = 0; i < Iterations; i++)
            {
                var result = loadBalancer.PickEndpoint(endpoints.ToList().AsReadOnly(), options);
                if (result == second)
                {
                    selections++;
                }
            }

            const int DeviationPercentage = 15;
            var target = Iterations / (int)(variation * 100);
            var deviation = target * DeviationPercentage / 100;
            Assert.InRange(selections, target - deviation, target + deviation);
        }

        internal class TestRandomFactory : IRandomFactory
        {
            internal TestRandom Instance { get; set; }

            public Random CreateRandomInstance()
            {
                return Instance;
            }
        }

        public class TestRandom : Random
        {
            public int[] Sequence { get; set; }
            public int Offset { get; set; }

            public override int Next(int maxValue)
            {
                return Sequence[Offset++];
            }
        }
    }
}

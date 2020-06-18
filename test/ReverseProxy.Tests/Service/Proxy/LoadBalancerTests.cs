// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Proxy.Tests
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
        public void PickDestination_WithoutDestinations_Null()
        {
            var loadBalancer = Create<LoadBalancer>();
            var destinations = new DestinationInfo[0];
            var options = new ClusterConfig.ClusterLoadBalancingOptions((LoadBalancingMode)(-1));

            var result = loadBalancer.PickDestination(destinations, in options);

            Assert.Null(result);
        }

        [Fact]
        public void PickDestination_SingleDestinations_ShortCircuit()
        {
            var loadBalancer = Create<LoadBalancer>();
            var destinations = new[]
            {
                new DestinationInfo("d1"),
            };
            var options = new ClusterConfig.ClusterLoadBalancingOptions((LoadBalancingMode)(-1));

            var result = loadBalancer.PickDestination(destinations, in options);

            Assert.Same(destinations[0], result);
        }

        [Fact]
        public void PickDestination_UnsupportedMode_Throws()
        {
            var loadBalancer = Create<LoadBalancer>();
            var destinations = new[]
            {
                new DestinationInfo("d1"),
                new DestinationInfo("d2"),
            };
            var options = new ClusterConfig.ClusterLoadBalancingOptions((LoadBalancingMode)(-1));

            Action action = () => loadBalancer.PickDestination(destinations, in options);

            // Assert
            var ex = Assert.Throws<NotSupportedException>(action);
            Assert.Equal("Load balancing mode '-1' is not supported.", ex.Message);
        }

        [Fact]
        public void PickDestination_FirstWithDestinations_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var destinations = new[]
            {
                new DestinationInfo("d1"),
                new DestinationInfo("d2"),
            };
            var options = new ClusterConfig.ClusterLoadBalancingOptions(LoadBalancingMode.First);

            var result = loadBalancer.PickDestination(destinations, in options);

            Assert.Same(destinations[0], result);
        }

        [Fact]
        public void PickDestination_Random_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var destinations = new[]
            {
                new DestinationInfo("d1"),
                new DestinationInfo("d2"),
            };
            RandomInstance.Sequence = new[] { 1 };
            var options = new ClusterConfig.ClusterLoadBalancingOptions(LoadBalancingMode.Random);

            var result = loadBalancer.PickDestination(destinations, in options);

            Assert.Same(result, destinations[1]);
        }

        [Fact]
        public void PickDestination_PowerOfTwoChoices_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var destinations = new[]
            {
                new DestinationInfo("d1"),
                new DestinationInfo("d2"),
            };
            destinations[0].ConcurrencyCounter.Increment();
            RandomInstance.Sequence = new[] { 1, 0 };
            var options = new ClusterConfig.ClusterLoadBalancingOptions(LoadBalancingMode.PowerOfTwoChoices);

            var result = loadBalancer.PickDestination(destinations, in options);

            Assert.Same(result, destinations[1]);
        }

        [Fact]
        public void PickDestination_LeastRequests_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var destinations = new[]
            {
                new DestinationInfo("d1"),
                new DestinationInfo("d2"),
            };
            destinations[0].ConcurrencyCounter.Increment();
            var options = new ClusterConfig.ClusterLoadBalancingOptions(LoadBalancingMode.LeastRequests);

            var result = loadBalancer.PickDestination(destinations, in options);

            Assert.Same(result, destinations[1]);
        }

        [Fact]
        public void PickDestination_RoundRobin_Works()
        {
            var loadBalancer = Create<LoadBalancer>();
            var destinations = new[]
            {
                new DestinationInfo("d1"),
                new DestinationInfo("d2"),
            };
            destinations[0].ConcurrencyCounter.Increment();
            var options = new ClusterConfig.ClusterLoadBalancingOptions(LoadBalancingMode.RoundRobin);

            var result0 = loadBalancer.PickDestination(destinations, in options);
            var result1 = loadBalancer.PickDestination(destinations, in options);
            var result2 = loadBalancer.PickDestination(destinations, in options);
            var result3 = loadBalancer.PickDestination(destinations, in options);

            Assert.Same(result0, destinations[0]);
            Assert.Same(result1, destinations[1]);
            Assert.Same(result2, destinations[0]);
            Assert.Same(result3, destinations[1]);
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

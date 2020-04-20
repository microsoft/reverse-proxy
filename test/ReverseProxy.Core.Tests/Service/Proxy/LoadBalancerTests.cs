// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;
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

        internal class TestRandomFactory : IRandomFactory
        {
            internal TestRandom Instance { get; set; }

            public IRandom CreateRandomInstance()
            {
                return Instance;
            }
        }

        public class TestRandom : IRandom
        {
            public int[] Sequence { get; set; }
            public int Offset { get; set; }

            public int Next()
            {
                return Sequence[Offset++];
            }

            public int Next(int maxValue)
            {
                return Sequence[Offset++];
            }

            public int Next(int minValue, int maxValue)
            {
                return Sequence[Offset++];
            }
        }
    }
}

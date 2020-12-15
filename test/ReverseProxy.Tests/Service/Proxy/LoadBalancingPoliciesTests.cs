// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.LoadBalancing;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Utilities;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Proxy.Tests
{
    public class LoadBalancingPoliciesTests : TestAutoMockBase
    {
        public LoadBalancingPoliciesTests()
        {
            RandomFactory = new TestRandomFactory() { Instance = RandomInstance };
            Provide<IRandomFactory>(RandomFactory);
        }

        private TestRandom RandomInstance { get; set; } = new TestRandom();

        private TestRandomFactory RandomFactory { get; set; }

        [Fact]
        public void PickDestination_FirstWithDestinations_Works()
        {
            var destinations = new[]
            {
                new DestinationInfo("d1"),
                new DestinationInfo("d2"),
                new DestinationInfo("d3")
            };

            var context = new DefaultHttpContext();
            var loadBalancer = Create<FirstLoadBalancingPolicy>();

            for (var i = 0; i < 10; i++)
            {
                var result = loadBalancer.PickDestination(context, destinations);
                Assert.Same(destinations[0], result);
                result.ConcurrencyCounter.Increment();
            }
        }

        [Fact]
        public void PickDestination_Random_Works()
        {
            var destinations = new[]
            {
                new DestinationInfo("d1"),
                new DestinationInfo("d2"),
                new DestinationInfo("d3")
            };

            const int Iterations = 10;
            var random = new Random(42);
            RandomInstance.Sequence = Enumerable.Range(0, Iterations).Select(_ => random.Next(destinations.Length)).ToArray();

            var context = new DefaultHttpContext();
            var loadBalancer = Create<RandomLoadBalancingPolicy>();

            for (var i = 0; i < Iterations; i++)
            {
                var result = loadBalancer.PickDestination(context, destinations);
                Assert.Same(destinations[RandomInstance.Sequence[i]], result);
                result.ConcurrencyCounter.Increment();
            }
        }

        [Fact]
        public void PickDestination_PowerOfTwoChoices_Works()
        {
            var destinations = new[]
            {
                new DestinationInfo("d1"),
                new DestinationInfo("d2"),
                new DestinationInfo("d3")
            };
            destinations[0].ConcurrencyCounter.Increment();

            const int Iterations = 10;
            var random = new Random(42);
            RandomInstance.Sequence = Enumerable.Range(0, Iterations * 2).Select(_ => random.Next(destinations.Length)).ToArray();

            var context = new DefaultHttpContext();
            var loadBalancer = Create<PowerOfTwoChoicesLoadBalancingPolicy>();

            for (var i = 0; i < Iterations; i++)
            {
                var result = loadBalancer.PickDestination(context, destinations);
                var first = destinations[RandomInstance.Sequence[i * 2]];
                var second = destinations[RandomInstance.Sequence[i * 2 + 1]];
                var expected = first.ConcurrentRequestCount <= second.ConcurrentRequestCount ? first : second;
                Assert.Same(expected, result);
                result.ConcurrencyCounter.Increment();
            }
        }

        [Fact]
        public void PickDestination_LeastRequests_Works()
        {
            var destinations = new[]
            {
                new DestinationInfo("d1"),
                new DestinationInfo("d2"),
                new DestinationInfo("d3")
            };
            destinations[0].ConcurrencyCounter.Increment();

            var context = new DefaultHttpContext();
            var loadBalancer = Create<LeastRequestsLoadBalancingPolicy>();

            for (var i = 0; i < 10; i++)
            {
                var result = loadBalancer.PickDestination(context, destinations);
                Assert.Same(destinations.OrderBy(d => d.ConcurrentRequestCount).First(), result);
                result.ConcurrencyCounter.Increment();
            }
        }

        [Fact]
        public void PickDestination_RoundRobin_Works()
        {
            var destinations = new[]
            {
                new DestinationInfo("d1"),
                new DestinationInfo("d2"),
                new DestinationInfo("d3")
            };
            destinations[0].ConcurrencyCounter.Increment();

            var context = new DefaultHttpContext();

            var routeConfig = new RouteConfig(new RouteInfo("route-1"), new ProxyRoute(), new ClusterInfo("cluster1", new DestinationManager()), transforms: null);
            var endpoint = new Endpoint(default, new EndpointMetadataCollection(routeConfig), string.Empty);
            context.SetEndpoint(endpoint);

            var loadBalancer = Create<RoundRobinLoadBalancingPolicy>();

            for (var i = 0; i < 10; i++)
            {
                var result = loadBalancer.PickDestination(context, destinations);
                Assert.Same(destinations[i % destinations.Length], result);
                result.ConcurrencyCounter.Increment();
            }
        }
    }
}

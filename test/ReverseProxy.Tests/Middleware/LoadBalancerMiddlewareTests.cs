// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service.LoadBalancing;
using Yarp.ReverseProxy.Service.Management;

namespace Yarp.ReverseProxy.Middleware.Tests
{
    public class LoadBalancerMiddlewareTests
    {
        private static LoadBalancingMiddleware CreateMiddleware(RequestDelegate next, params ILoadBalancingPolicy[] loadBalancingPolicies)
        {
            var logger = new Mock<ILogger<LoadBalancingMiddleware>>();
            logger
                .Setup(l => l.IsEnabled(It.IsAny<LogLevel>()))
                .Returns(true);

            return new LoadBalancingMiddleware(
                next,
                logger.Object,
                loadBalancingPolicies);
        }

        [Fact]
        public void Constructor_Works()
        {
            CreateMiddleware(_ => Task.CompletedTask);
        }

        [Fact]
        public async Task PickDestination_UnsupportedPolicy_Throws()
        {
            const string PolicyName = "NonExistentPolicy";
            var context = CreateContext(PolicyName, new[]
            {
                new DestinationInfo("destination1"),
                new DestinationInfo("destination2")
            });

            var sut = CreateMiddleware(_ => Task.CompletedTask);

            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await sut.Invoke(context));
            Assert.Equal($"No {typeof(ILoadBalancingPolicy)} was found for the id '{PolicyName}'. (Parameter 'id')", ex.Message);
        }

        [Fact]
        public async Task PickDestination_SingleDestinations_ShortCircuit()
        {
            var context = CreateContext(LoadBalancingPolicies.First, new[]
            {
                new DestinationInfo("destination1")
            });

            var sut = CreateMiddleware(_ => Task.CompletedTask);

            await sut.Invoke(context);

            var feature = context.Features.Get<IReverseProxyFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.AvailableDestinations);
            Assert.Equal(1, feature.AvailableDestinations.Count);
            Assert.Same("destination1", feature.AvailableDestinations[0].DestinationId);

            Assert.Equal(200, context.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_Works()
        {
            var context = CreateContext(LoadBalancingPolicies.First, new[]
            {
                new DestinationInfo("destination1"),
                new DestinationInfo("destination2")
            });

            var sut = CreateMiddleware(_ => Task.CompletedTask, new FirstLoadBalancingPolicy());

            await sut.Invoke(context);

            var feature = context.Features.Get<IReverseProxyFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.AvailableDestinations);
            Assert.Equal(1, feature.AvailableDestinations.Count);
            Assert.Same("destination1", feature.AvailableDestinations[0].DestinationId);

            Assert.Equal(200, context.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_WithoutDestinations_503()
        {
            var context = CreateContext(LoadBalancingPolicies.First, Array.Empty<DestinationInfo>());

            var sut = CreateMiddleware(context =>
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return Task.CompletedTask;
            });

            await sut.Invoke(context);

            var feature = context.Features.Get<IReverseProxyFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.AvailableDestinations);
            Assert.Equal(0, feature.AvailableDestinations.Count);

            Assert.Equal(503, context.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_ServiceReturnsNoResults_FallThrough()
        {
            const string PolicyName = "CustomPolicy";

            var context = CreateContext(PolicyName, new[]
            {
                new DestinationInfo("destination1"),
                new DestinationInfo("destination2")
            });

            var policy = new Mock<ILoadBalancingPolicy>();
            policy
                .Setup(p => p.Name)
                .Returns(PolicyName);
            policy
                .Setup(p => p.PickDestination(It.IsAny<HttpContext>(), It.IsAny<IReadOnlyList<DestinationInfo>>()))
                .Returns((DestinationInfo)null);

            var sut = CreateMiddleware(context =>
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return Task.CompletedTask;
            },
            policy.Object);

            await sut.Invoke(context);

            var feature = context.Features.Get<IReverseProxyFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.AvailableDestinations);
            Assert.Equal(0, feature.AvailableDestinations.Count);

            Assert.Equal(503, context.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_NoPolicySpecified_DefaultsToPowerOfTwoChoices()
        {
            var destinations = new[]
            {
                new DestinationInfo("destination1"),
                new DestinationInfo("destination2")
            };
            var context = CreateContext(loadBalancingPolicy: null, destinations);

            var policy = new Mock<ILoadBalancingPolicy>();
            policy
                .Setup(p => p.Name)
                .Returns(LoadBalancingPolicies.PowerOfTwoChoices);
            policy
                .Setup(p => p.PickDestination(It.IsAny<HttpContext>(), It.IsAny<IReadOnlyList<DestinationInfo>>()))
                .Returns(destinations[0]);

            var sut = CreateMiddleware(_ => Task.CompletedTask, policy.Object);

            await sut.Invoke(context);

            policy.Verify(p => p.PickDestination(context, destinations), Times.Once);
        }

        private static HttpContext CreateContext(string loadBalancingPolicy, IReadOnlyList<DestinationInfo> destinations)
        {
            var cluster = new ClusterState("cluster1")
            {
                Model = new ClusterModel(new ClusterConfig { LoadBalancingPolicy = loadBalancingPolicy }, default)
            };

            var context = new DefaultHttpContext();

            context.Features.Set<IReverseProxyFeature>(
                new ReverseProxyFeature()
                {
                    AvailableDestinations = destinations,
                    Cluster = cluster.Model
                });
            context.Features.Set(cluster);

            var routeConfig = new RouteModel(new RouteConfig(), cluster, transformer: null);
            var endpoint = new Endpoint(default, new EndpointMetadataCollection(routeConfig), string.Empty);
            context.SetEndpoint(endpoint);

            return context;
        }
    }
}

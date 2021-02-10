// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.LoadBalancing;
using Microsoft.ReverseProxy.Service.Management;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Middleware.Tests
{
    public class LoadBalancerMiddlewareTests
    {
        private static LoadBalancingMiddleware CreateMiddleware()
        {
            var logger = new Mock<ILogger<LoadBalancingMiddleware>>();
            logger
                .Setup(l => l.IsEnabled(It.IsAny<LogLevel>()))
                .Returns(true);

            return new LoadBalancingMiddleware(
                context => Task.CompletedTask,
                logger.Object);
        }

        [Fact]
        public void Constructor_Works()
        {
            CreateMiddleware();
        }

        [Fact]
        public async Task Invoke_Works()
        {
            var context = CreateContext(new FirstLoadBalancingPolicy(), new[]
            {
                new DestinationInfo("destination1"),
                new DestinationInfo("destination2")
            });

            var sut = CreateMiddleware();

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
            var context = CreateContext(new FirstLoadBalancingPolicy(), Array.Empty<DestinationInfo>());

            var sut = CreateMiddleware();

            await sut.Invoke(context);

            var feature = context.Features.Get<IReverseProxyFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.AvailableDestinations);
            Assert.Equal(0, feature.AvailableDestinations.Count); // Unmodified

            Assert.Equal(503, context.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_ServiceReturnsNoResults_503()
        {
            const string PolicyName = "CustomPolicy";

            var policy = new Mock<ILoadBalancingPolicy>();
            policy
                .Setup(p => p.Name)
                .Returns(PolicyName);
            policy
                .Setup(p => p.PickDestination(It.IsAny<HttpContext>(), It.IsAny<IReadOnlyList<DestinationInfo>>()))
                .Returns((DestinationInfo)null);

            var context = CreateContext(policy.Object, new[]
            {
                new DestinationInfo("destination1"),
                new DestinationInfo("destination2")
            });

            

            var sut = CreateMiddleware();

            await sut.Invoke(context);

            var feature = context.Features.Get<IReverseProxyFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.AvailableDestinations);
            Assert.Equal(2, feature.AvailableDestinations.Count); // Unmodified

            Assert.Equal(503, context.Response.StatusCode);
        }

        private static HttpContext CreateContext(ILoadBalancingPolicy loadBalancingPolicy, IReadOnlyList<DestinationInfo> destinations)
        {
            var cluster = new ClusterInfo("cluster1", new DestinationManager())
            {
                Config = new ClusterConfig(new Cluster(), default, loadBalancingPolicy)
            };

            var context = new DefaultHttpContext();

            context.Features.Set<IReverseProxyFeature>(
                new ReverseProxyFeature()
                {
                    AvailableDestinations = destinations,
                    ClusterConfig = cluster.Config
                });
            context.Features.Set(cluster);

            var routeConfig = new RouteConfig(new RouteInfo("route-1"), new ProxyRoute(), cluster, transformer: null);
            var endpoint = new Endpoint(default, new EndpointMetadataCollection(routeConfig), string.Empty);
            context.SetEndpoint(endpoint);

            return context;
        }
    }
}

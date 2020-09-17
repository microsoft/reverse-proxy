// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Telemetry;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Middleware.Tests
{
    public class LoadBalancerMiddlewareTests : TestAutoMockBase
    {
        public LoadBalancerMiddlewareTests()
        {
            Provide<IOperationLogger<LoadBalancingMiddleware>, TextOperationLogger<LoadBalancingMiddleware>>();
            Provide<RequestDelegate>(context => Task.CompletedTask);
        }

        [Fact]
        public void Constructor_Works()
        {
            Create<LoadBalancingMiddleware>();
        }

        [Fact]
        public async Task Invoke_Works()
        {
            var httpClient = new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object);
            var cluster1 = new ClusterInfo(
                clusterId: "cluster1",
                destinationManager: new DestinationManager());
            cluster1.Config.Value = new ClusterConfig(default, default, new ClusterConfig.ClusterLoadBalancingOptions(LoadBalancingMode.RoundRobin), default, httpClient, default, new Dictionary<string, string>());
            var destination1 = cluster1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.ConfigSignal.Value = new DestinationConfig("https://localhost:123/a/b/");
                    destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Healthy);
                });
            var destination2 = cluster1.DestinationManager.GetOrCreateItem(
                "destination2",
                destination =>
                {
                    destination.ConfigSignal.Value = new DestinationConfig("https://localhost:123/a/b/");
                    destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Healthy);
                });

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
                route: new RouteInfo("route1"),
                proxyRoute: new ProxyRoute(),
                cluster: cluster1,
                aspNetCoreEndpoints: aspNetCoreEndpoints.AsReadOnly(),
                transforms: null);
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            var httpContext = new DefaultHttpContext();
            httpContext.SetEndpoint(aspNetCoreEndpoint);
            Provide<ILoadBalancer, LoadBalancer>();

            httpContext.Features.Set<IReverseProxyFeature>(
                new ReverseProxyFeature()
                {
                    AvailableDestinations = new List<DestinationInfo>() { destination1, destination2 },
                    ClusterConfig = cluster1.Config.Value
                });
            httpContext.Features.Set(cluster1);

            var sut = Create<LoadBalancingMiddleware>();

            await sut.Invoke(httpContext);

            var feature = httpContext.Features.Get<IReverseProxyFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.AvailableDestinations);
            Assert.Equal(1, feature.AvailableDestinations.Count);
            Assert.Same(destination1, feature.AvailableDestinations[0]);

            Assert.Equal(200, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_ServiceReturnsNoResults_503()
        {
            var httpClient = new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object);
            var cluster1 = new ClusterInfo(
                clusterId: "cluster1",
                destinationManager: new DestinationManager());
            cluster1.Config.Value = new ClusterConfig(default, default, new ClusterConfig.ClusterLoadBalancingOptions(LoadBalancingMode.RoundRobin), default, httpClient, default, new Dictionary<string, string>());
            var destination1 = cluster1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.ConfigSignal.Value = new DestinationConfig("https://localhost:123/a/b/");
                    destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Healthy);
                });
            var destination2 = cluster1.DestinationManager.GetOrCreateItem(
                "destination2",
                destination =>
                {
                    destination.ConfigSignal.Value = new DestinationConfig("https://localhost:123/a/b/");
                    destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Healthy);
                });

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
                route: new RouteInfo("route1"),
                proxyRoute: new ProxyRoute(),
                cluster: cluster1,
                aspNetCoreEndpoints: aspNetCoreEndpoints.AsReadOnly(),
                transforms: null);
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            var httpContext = new DefaultHttpContext();
            httpContext.SetEndpoint(aspNetCoreEndpoint);

            Mock<ILoadBalancer>()
                .Setup(l => l.PickDestination(It.IsAny<IReadOnlyList<DestinationInfo>>(), It.IsAny<ClusterConfig.ClusterLoadBalancingOptions>()))
                .Returns((DestinationInfo)null);

            httpContext.Features.Set<IReverseProxyFeature>(
                new ReverseProxyFeature()
                {
                    AvailableDestinations = new List<DestinationInfo>() { destination1, destination2 }.AsReadOnly(),
                    ClusterConfig = cluster1.Config.Value
                });
            httpContext.Features.Set(cluster1);

            var sut = Create<LoadBalancingMiddleware>();

            await sut.Invoke(httpContext);

            var feature = httpContext.Features.Get<IReverseProxyFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.AvailableDestinations);
            Assert.Equal(2, feature.AvailableDestinations.Count); // Unmodified

            Assert.Equal(503, httpContext.Response.StatusCode);
        }

        private static Endpoint CreateAspNetCoreEndpoint(RouteConfig routeConfig)
        {
            var endpointBuilder = new RouteEndpointBuilder(
                requestDelegate: httpContext => Task.CompletedTask,
                routePattern: RoutePatternFactory.Parse("/"),
                order: 0);
            endpointBuilder.Metadata.Add(routeConfig);
            return endpointBuilder.Build();
        }
    }
}

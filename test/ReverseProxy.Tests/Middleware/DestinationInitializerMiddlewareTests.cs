// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Middleware.Tests
{
    public class DestinationInitializerMiddlewareTests : TestAutoMockBase
    {
        public DestinationInitializerMiddlewareTests()
        {
            Provide<RequestDelegate>(context => Task.CompletedTask);
        }

        [Fact]
        public void Constructor_Works()
        {
            Create<DestinationInitializerMiddleware>();
        }
        
        [Fact]
        public async Task Invoke_SetsFeatures()
        {
            var proxyHttpClientFactoryMock = new Mock<IProxyHttpClientFactory>();
            var cluster1 = new ClusterInfo(
                clusterId: "cluster1",
                destinationManager: new DestinationManager(),
                proxyHttpClientFactory: proxyHttpClientFactoryMock.Object);
            var destination1 = cluster1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.ConfigSignal.Value = new DestinationConfig("https://localhost:123/a/b/");
                    destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Healthy);
                });

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
                new RouteInfo("route1"),
                configHash: 0,
                priority: null,
                cluster1,
                aspNetCoreEndpoints.AsReadOnly(),
                transforms: null);
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            var httpContext = new DefaultHttpContext();
            httpContext.SetEndpoint(aspNetCoreEndpoint);

            var sut = Create<DestinationInitializerMiddleware>();

            await sut.Invoke(httpContext);

            var feature = httpContext.Features.Get<IAvailableDestinationsFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.Destinations);
            Assert.Equal(1, feature.Destinations.Count);
            Assert.Same(destination1, feature.Destinations[0]);

            var cluster = httpContext.Features.Get<ClusterInfo>();
            Assert.Same(cluster1, cluster);

            Assert.Equal(200, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_NoHealthyEndpoints_503()
        {
            var proxyHttpClientFactoryMock = new Mock<IProxyHttpClientFactory>();
            var cluster1 = new ClusterInfo(
                clusterId: "cluster1",
                destinationManager: new DestinationManager(),
                proxyHttpClientFactory: proxyHttpClientFactoryMock.Object);
            cluster1.Config.Value = new ClusterConfig(
                new ClusterConfig.ClusterHealthCheckOptions(enabled: true, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan, 0, ""),
                new ClusterConfig.ClusterLoadBalancingOptions(),
                new ClusterConfig.ClusterSessionAffinityOptions());
            var destination1 = cluster1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.ConfigSignal.Value = new DestinationConfig("https://localhost:123/a/b/");
                    destination.DynamicStateSignal.Value = new DestinationDynamicState(DestinationHealth.Unhealthy);
                });

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
                route: new RouteInfo("route1"),
                configHash: 0,
                priority: null,
                cluster: cluster1,
                aspNetCoreEndpoints: aspNetCoreEndpoints.AsReadOnly(),
                transforms: null);
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            var httpContext = new DefaultHttpContext();
            httpContext.SetEndpoint(aspNetCoreEndpoint);

            var sut = Create<DestinationInitializerMiddleware>();

            await sut.Invoke(httpContext);

            var feature = httpContext.Features.Get<IAvailableDestinationsFeature>();
            Assert.Null(feature);

            var cluster = httpContext.Features.Get<ClusterInfo>();
            Assert.Null(cluster);

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

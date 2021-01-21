// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy;
using Moq;
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
            var httpClient = new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object);
            var cluster1 = new ClusterInfo(
                clusterId: "cluster1",
                destinationManager: new DestinationManager());
            cluster1.Config = new ClusterConfig(new Cluster(), httpClient, default, new Dictionary<string, string>());
            var destination1 = cluster1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.Config = new DestinationConfig("https://localhost:123/a/b/", null);
                });
            cluster1.UpdateDynamicState();

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
                new RouteInfo("route1"),
                proxyRoute: new ProxyRoute(),
                cluster1,
                transformer: null);
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            var httpContext = new DefaultHttpContext();
            httpContext.SetEndpoint(aspNetCoreEndpoint);

            var sut = Create<DestinationInitializerMiddleware>();

            await sut.Invoke(httpContext);

            var proxyFeature = httpContext.GetRequiredProxyFeature();
            Assert.NotNull(proxyFeature);
            Assert.NotNull(proxyFeature.AvailableDestinations);
            Assert.Equal(1, proxyFeature.AvailableDestinations.Count);
            Assert.Same(destination1, proxyFeature.AvailableDestinations[0]);
            Assert.Same(cluster1.Config, proxyFeature.ClusterConfig);

            Assert.Equal(200, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_NoHealthyEndpoints_503()
        {
            var httpClient = new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object);
            var cluster1 = new ClusterInfo(
                clusterId: "cluster1",
                destinationManager: new DestinationManager());
            cluster1.Config = new ClusterConfig(
                new Cluster()
                {
                    HealthCheck = new HealthCheckOptions
                    {
                        Active = new ActiveHealthCheckOptions
                        {
                            Enabled = true,
                            Timeout = Timeout.InfiniteTimeSpan,
                            Interval = Timeout.InfiniteTimeSpan,
                            Policy = "Any5xxResponse",
                        }
                    }
                },
                httpClient,
                new ClusterProxyHttpClientOptions(),
                new Dictionary<string, string>());
            var destination1 = cluster1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.Config = new DestinationConfig("https://localhost:123/a/b/", null);
                    destination.Health.Passive = DestinationHealth.Unhealthy;
                });

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
                route: new RouteInfo("route1"),
                proxyRoute: new ProxyRoute(),
                cluster: cluster1,
                transformer: null);
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            var httpContext = new DefaultHttpContext();
            httpContext.SetEndpoint(aspNetCoreEndpoint);

            var sut = Create<DestinationInitializerMiddleware>();

            await sut.Invoke(httpContext);

            var feature = httpContext.Features.Get<IReverseProxyFeature>();
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

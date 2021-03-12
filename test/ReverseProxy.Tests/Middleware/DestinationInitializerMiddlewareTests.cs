// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Common.Tests;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service.Management;

namespace Yarp.ReverseProxy.Middleware.Tests
{
    public class DestinationInitializerMiddlewareTests : TestAutoMockBase
    {
        public DestinationInitializerMiddlewareTests()
        {
            Provide<RequestDelegate>(context =>
            {
                context.Response.StatusCode = StatusCodes.Status418ImATeapot;
                return Task.CompletedTask;
            });
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
            cluster1.Config = new ClusterConfig(new Cluster(), httpClient);
            var destination1 = cluster1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.Config = new DestinationConfig(new Destination { Address = "https://localhost:123/a/b/" });
                });
            cluster1.UpdateDynamicState();

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
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
            Assert.Same(cluster1.Config, proxyFeature.ClusterSnapshot);

            Assert.Equal(StatusCodes.Status418ImATeapot, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_NoHealthyEndpoints_CallsNext()
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
                httpClient);
            var destination1 = cluster1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.Config = new DestinationConfig(new Destination { Address = "https://localhost:123/a/b/" });
                    destination.Health.Active = DestinationHealth.Unhealthy;
                });
            cluster1.UpdateDynamicState();

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
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
            Assert.NotNull(feature);
            Assert.Single(feature.AllDestinations);
            Assert.Empty(feature.AvailableDestinations);

            Assert.Equal(StatusCodes.Status418ImATeapot, httpContext.Response.StatusCode);
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

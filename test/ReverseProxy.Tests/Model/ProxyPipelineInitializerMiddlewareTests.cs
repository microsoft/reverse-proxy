// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Common.Tests;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Proxy;

namespace Yarp.ReverseProxy.Model.Tests
{
    public class ProxyPipelineInitializerMiddlewareTests : TestAutoMockBase
    {
        public ProxyPipelineInitializerMiddlewareTests()
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
            Create<ProxyPipelineInitializerMiddleware>();
        }

        [Fact]
        public async Task Invoke_SetsFeatures()
        {
            var httpClient = new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object);
            var cluster1 = new ClusterState(clusterId: "cluster1");
            cluster1.Model = new ClusterModel(new ClusterConfig(), httpClient);
            var destination1 = cluster1.Destinations.GetOrAdd(
                "destination1",
                id => new DestinationState(id) { Model = new DestinationModel(new DestinationConfig { Address = "https://localhost:123/a/b/" }) });
            cluster1.DestinationsState = new ClusterDestinationsState(new[] { destination1 }, new[] { destination1 });

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteModel(
                config: new RouteConfig(),
                cluster1,
                HttpTransformer.Default);
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            var httpContext = new DefaultHttpContext();
            httpContext.SetEndpoint(aspNetCoreEndpoint);

            var sut = Create<ProxyPipelineInitializerMiddleware>();

            await sut.Invoke(httpContext);

            var proxyFeature = httpContext.GetReverseProxyFeature();
            Assert.NotNull(proxyFeature);
            Assert.NotNull(proxyFeature.AvailableDestinations);
            Assert.Equal(1, proxyFeature.AvailableDestinations.Count);
            Assert.Same(destination1, proxyFeature.AvailableDestinations[0]);
            Assert.Same(cluster1.Model, proxyFeature.Cluster);

            Assert.Equal(StatusCodes.Status418ImATeapot, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_NoHealthyEndpoints_CallsNext()
        {
            var httpClient = new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object);
            var cluster1 = new ClusterState(clusterId: "cluster1");
            cluster1.Model = new ClusterModel(
                new ClusterConfig()
                {
                    HealthCheck = new HealthCheckConfig
                    {
                        Active = new ActiveHealthCheckConfig
                        {
                            Enabled = true,
                            Timeout = Timeout.InfiniteTimeSpan,
                            Interval = Timeout.InfiniteTimeSpan,
                            Policy = "Any5xxResponse",
                        }
                    }
                },
                httpClient);
            var destination1 = cluster1.Destinations.GetOrAdd(
                "destination1",
                id => new DestinationState(id)
                {
                    Model = new DestinationModel(new DestinationConfig { Address = "https://localhost:123/a/b/" }),
                    Health = { Active = DestinationHealth.Unhealthy },
                });
            cluster1.DestinationsState = new ClusterDestinationsState(new[] { destination1 }, Array.Empty<DestinationState>());

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteModel(
                config: new RouteConfig(),
                cluster: cluster1,
                transformer: HttpTransformer.Default);
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            var httpContext = new DefaultHttpContext();
            httpContext.SetEndpoint(aspNetCoreEndpoint);

            var sut = Create<ProxyPipelineInitializerMiddleware>();

            await sut.Invoke(httpContext);

            var feature = httpContext.Features.Get<IReverseProxyFeature>();
            Assert.NotNull(feature);
            Assert.Single(feature.AllDestinations, destination1);
            Assert.Empty(feature.AvailableDestinations);

            Assert.Equal(StatusCodes.Status418ImATeapot, httpContext.Response.StatusCode);
        }

        private static Endpoint CreateAspNetCoreEndpoint(RouteModel routeConfig)
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

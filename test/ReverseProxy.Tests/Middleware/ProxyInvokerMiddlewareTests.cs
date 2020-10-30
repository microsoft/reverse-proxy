// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
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
    public class ProxyInvokerMiddlewareTests : TestAutoMockBase
    {
        public ProxyInvokerMiddlewareTests()
        {
            Provide<IOperationLogger<ProxyInvokerMiddleware>, TextOperationLogger<ProxyInvokerMiddleware>>();
        }

        [Fact]
        public void Constructor_Works()
        {
            Create<ProxyInvokerMiddleware>();
        }

        [Fact]
        public async Task Invoke_Works()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("example.com");
            httpContext.Request.Path = "/api/test";
            httpContext.Request.QueryString = new QueryString("?a=b&c=d");

            var httpClient = new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object);
            var cluster1 = new ClusterInfo(
                clusterId: "cluster1",
                destinationManager: new DestinationManager());
            var clusterConfig = new ClusterConfig(default, default, default, default, httpClient, default, new Dictionary<string, string>());
            var destination1 = cluster1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.Config = new DestinationConfig("https://localhost:123/a/b/", null);
                    destination.DynamicState = new DestinationDynamicState(new CompositeDestinationHealth(DestinationHealth.Healthy, DestinationHealth.Unknown));
                });
            httpContext.Features.Set<IReverseProxyFeature>(
                new ReverseProxyFeature() { AvailableDestinations = new List<DestinationInfo>() { destination1 }.AsReadOnly(), ClusterConfig = clusterConfig });
            httpContext.Features.Set(cluster1);

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
                route: new RouteInfo("route1"),
                proxyRoute: new ProxyRoute(),
                cluster: cluster1,
                aspNetCoreEndpoints: aspNetCoreEndpoints.AsReadOnly(),
                transforms: null);
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            httpContext.SetEndpoint(aspNetCoreEndpoint);

            var tcs1 = new TaskCompletionSource<bool>();
            var tcs2 = new TaskCompletionSource<bool>();
            Mock<IHttpProxy>()
                .Setup(h => h.ProxyAsync(
                    httpContext,
                    It.Is<string>(uri => uri == "https://localhost:123/a/b/"),
                    httpClient,
                    It.IsAny<RequestProxyOptions>()))
                .Returns(
                    async () =>
                    {
                        tcs1.TrySetResult(true);
                        await tcs2.Task;
                    })
                .Verifiable();

            var sut = Create<ProxyInvokerMiddleware>();

            // Act
            Assert.Equal(0, cluster1.ConcurrencyCounter.Value);
            Assert.Equal(0, destination1.ConcurrencyCounter.Value);

            var task = sut.Invoke(httpContext);
            if (task.IsFaulted)
            {
                // Something went wrong, don't hang the test.
                await task;
            }
            await tcs1.Task; // Wait until we get to the proxying step.
            Assert.Equal(1, cluster1.ConcurrencyCounter.Value);
            Assert.Equal(1, destination1.ConcurrencyCounter.Value);

            Assert.Same(destination1, httpContext.GetRequiredProxyFeature().SelectedDestination);

            tcs2.TrySetResult(true);
            await task;
            Assert.Equal(0, cluster1.ConcurrencyCounter.Value);
            Assert.Equal(0, destination1.ConcurrencyCounter.Value);

            // Assert
            Mock<IHttpProxy>().Verify();
        }

        [Fact]
        public async Task NoDestinations_503()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("example.com");

            var httpClient = new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object);
            var cluster1 = new ClusterInfo(
                clusterId: "cluster1",
                destinationManager: new DestinationManager());
            var clusterConfig = new ClusterConfig(default, default, default, default, httpClient, default, new Dictionary<string, string>());
            httpContext.Features.Set<IReverseProxyFeature>(
                new ReverseProxyFeature() { AvailableDestinations = Array.Empty<DestinationInfo>(), ClusterConfig = clusterConfig });
            httpContext.Features.Set(cluster1);

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
                route: new RouteInfo("route1"),
                proxyRoute: new ProxyRoute(),
                cluster: cluster1,
                aspNetCoreEndpoints: aspNetCoreEndpoints.AsReadOnly(),
                transforms: null);
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            httpContext.SetEndpoint(aspNetCoreEndpoint);

            Mock<IHttpProxy>()
                .Setup(h => h.ProxyAsync(
                    httpContext,
                    It.IsAny<string>(),
                    httpClient,
                    It.IsAny<RequestProxyOptions>()))
                .Returns(() => throw new NotImplementedException());

            var sut = Create<ProxyInvokerMiddleware>();

            Assert.Equal(0, cluster1.ConcurrencyCounter.Value);

            await sut.Invoke(httpContext);
            Assert.Equal(0, cluster1.ConcurrencyCounter.Value);

            Mock<IHttpProxy>().Verify();
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, httpContext.Response.StatusCode);
            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            Assert.Equal(ProxyError.NoAvailableDestinations, errorFeature?.Error);
            Assert.Null(errorFeature.Exception);
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

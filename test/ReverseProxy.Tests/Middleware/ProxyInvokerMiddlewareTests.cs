// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Middleware.Tests
{
    public class ProxyInvokerMiddlewareTests : TestAutoMockBase
    {
        [Fact]
        public void Constructor_Works()
        {
            Create<ProxyInvokerMiddleware>();
        }

        [Fact]
        public async Task Invoke_Works()
        {
            var events = TestEventListener.Collect();

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("example.com");
            httpContext.Request.Path = "/api/test";
            httpContext.Request.QueryString = new QueryString("?a=b&c=d");

            var httpClient = new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object);
            var httpRequestOptions = new RequestProxyOptions(
                TimeSpan.FromSeconds(60),
                HttpVersion.Version11
#if NET
                , HttpVersionPolicy.RequestVersionExact
#endif
            );
            var cluster1 = new ClusterInfo(
                clusterId: "cluster1",
                destinationManager: new DestinationManager());
            var clusterConfig = new ClusterConfig(default, default, default, default, httpClient, default, httpRequestOptions, new Dictionary<string, string>());
            var destination1 = cluster1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.Config = new DestinationConfig("https://localhost:123/a/b/", null);
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
                    It.Is<Transforms>(transforms => transforms == null),
                    It.Is<RequestProxyOptions>(requestOptions =>
                        requestOptions.Timeout == httpRequestOptions.Timeout
                        && requestOptions.Version == httpRequestOptions.Version
#if NET
                        && requestOptions.VersionPolicy == httpRequestOptions.VersionPolicy
#endif
                        )))
                .Returns(
                    async () =>
                    {
                        tcs1.TrySetResult(true);
                        await tcs2.Task;
                    })
                .Verifiable();

            var sut = Create<ProxyInvokerMiddleware>();

            Assert.Equal(0, cluster1.ConcurrencyCounter.Value);
            Assert.Equal(0, destination1.ConcurrencyCounter.Value);

            var task = sut.Invoke(httpContext);
            if (task.IsFaulted)
            {
                // Something went wrong, don't hang the test.
                await task;
            }

            Mock<IHttpProxy>().Verify();

            await tcs1.Task; // Wait until we get to the proxying step.
            Assert.Equal(1, cluster1.ConcurrencyCounter.Value);
            Assert.Equal(1, destination1.ConcurrencyCounter.Value);

            Assert.Same(destination1, httpContext.GetRequiredProxyFeature().SelectedDestination);

            tcs2.TrySetResult(true);
            await task;
            Assert.Equal(0, cluster1.ConcurrencyCounter.Value);
            Assert.Equal(0, destination1.ConcurrencyCounter.Value);

            var invoke = Assert.Single(events, e => e.EventName == "ProxyInvoke");
            Assert.Equal(3, invoke.Payload.Count);
            Assert.Equal(cluster1.ClusterId, (string)invoke.Payload[0]);
            Assert.Equal(routeConfig.Route.RouteId, (string)invoke.Payload[1]);
            Assert.Equal(destination1.DestinationId, (string)invoke.Payload[2]);
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
            var clusterConfig = new ClusterConfig(default, default, default, default, httpClient, default, default, new Dictionary<string, string>());
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
                    It.IsAny<Transforms>(),
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

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
using Moq;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Common.Tests;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service.Management;
using Yarp.ReverseProxy.Service.Proxy;

namespace Yarp.ReverseProxy.Middleware.Tests
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
            var httpRequestOptions = new RequestProxyOptions
            {
                Timeout = TimeSpan.FromSeconds(60),
                Version = HttpVersion.Version11,
#if NET
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
#endif
            };
            var cluster1 = new ClusterInfo(
                clusterId: "cluster1",
                destinationManager: new DestinationManager());
            var clusterConfig = new ClusterConfig(new Cluster() { HttpRequest = httpRequestOptions },
                httpClient);
            var destination1 = cluster1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.Config = new DestinationConfig(new Destination { Address = "https://localhost:123/a/b/" });
                });
            var routeConfig = new RouteConfig(
                proxyRoute: new ProxyRoute() { RouteId = "Route-1" },
                cluster: cluster1,
                transformer: null);

            httpContext.Features.Set<IReverseProxyFeature>(
                new ReverseProxyFeature()
            {
                    AvailableDestinations = new List<DestinationInfo>() { destination1 }.AsReadOnly(),
                    ClusterSnapshot = clusterConfig,
                    RouteSnapshot = routeConfig,
                });
            httpContext.Features.Set(cluster1);

            var tcs1 = new TaskCompletionSource<bool>();
            var tcs2 = new TaskCompletionSource<bool>();
            Mock<IHttpProxy>()
                .Setup(h => h.ProxyAsync(
                    httpContext,
                    It.Is<string>(uri => uri == "https://localhost:123/a/b/"),
                    httpClient,
                    It.Is<RequestProxyOptions>(requestOptions =>
                        requestOptions.Timeout == httpRequestOptions.Timeout
                        && requestOptions.Version == httpRequestOptions.Version
#if NET
                        && requestOptions.VersionPolicy == httpRequestOptions.VersionPolicy
#endif
                        ),
                    It.Is<HttpTransformer>(transformer => transformer == null)))
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

            Assert.Same(destination1, httpContext.GetRequiredProxyFeature().ProxiedDestination);

            tcs2.TrySetResult(true);
            await task;
            Assert.Equal(0, cluster1.ConcurrencyCounter.Value);
            Assert.Equal(0, destination1.ConcurrencyCounter.Value);

            var invoke = Assert.Single(events, e => e.EventName == "ProxyInvoke");
            Assert.Equal(3, invoke.Payload.Count);
            Assert.Equal(cluster1.ClusterId, (string)invoke.Payload[0]);
            Assert.Equal(routeConfig.ProxyRoute.RouteId, (string)invoke.Payload[1]);
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
            var clusterConfig = new ClusterConfig(new Cluster(), httpClient);
            var routeConfig = new RouteConfig(
                proxyRoute: new ProxyRoute(),
                cluster: cluster1,
                transformer: null);
            httpContext.Features.Set<IReverseProxyFeature>(
                new ReverseProxyFeature()
                {
                    AvailableDestinations = Array.Empty<DestinationInfo>(),
                    ClusterSnapshot = clusterConfig,
                    RouteSnapshot = routeConfig,
                });

            Mock<IHttpProxy>()
                .Setup(h => h.ProxyAsync(
                    httpContext,
                    It.IsAny<string>(),
                    httpClient,
                    It.IsAny<RequestProxyOptions>(),
                    It.IsAny<HttpTransformer>()))
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
    }
}

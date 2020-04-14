// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Common.Telemetry;
using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Core.Service.Management;
using Microsoft.ReverseProxy.Core.Service.Proxy;
using Microsoft.ReverseProxy.Core.Service.Proxy.Infra;
using Moq;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Middleware.Tests
{
    public class ProxyInvokerMiddlewareTests : TestAutoMockBase
    {
        public ProxyInvokerMiddlewareTests()
        {
            Provide<IOperationLogger, TextOperationLogger>();
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

            var proxyHttpClientFactoryMock = new Mock<IProxyHttpClientFactory>();
            var backend1 = new BackendInfo(
                backendId: "backend1",
                endpointManager: new EndpointManager(),
                proxyHttpClientFactory: proxyHttpClientFactoryMock.Object);
            var endpoint1 = backend1.EndpointManager.GetOrCreateItem(
                "endpoint1",
                endpoint =>
                {
                    endpoint.Config.Value = new EndpointConfig("https://localhost:123/a/b/");
                    endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Healthy);
                });
            httpContext.Features.Set<IAvailableBackendEndpointsFeature>(
                new AvailableBackendEndpointsFeature() { Endpoints = new List<EndpointInfo>() { endpoint1 }.AsReadOnly() });

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
                route: new RouteInfo("route1"),
                matcherSummary: null,
                priority: null,
                backendOrNull: backend1,
                aspNetCoreEndpoints: aspNetCoreEndpoints.AsReadOnly());
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            httpContext.SetEndpoint(aspNetCoreEndpoint);

            var tcs1 = new TaskCompletionSource<bool>();
            var tcs2 = new TaskCompletionSource<bool>();
            Mock<IHttpProxy>()
                .Setup(h => h.ProxyAsync(
                    httpContext,
                    It.Is<Uri>(uri => uri == new Uri("https://localhost:123/a/b/api/test?a=b&c=d")),
                    proxyHttpClientFactoryMock.Object,
                    It.Is<ProxyTelemetryContext>(ctx => ctx.BackendId == "backend1" && ctx.RouteId == "route1" && ctx.EndpointId == "endpoint1"),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<CancellationToken>()))
                .Returns(
                    async () =>
                    {
                        tcs1.TrySetResult(true);
                        await tcs2.Task;
                    })
                .Verifiable();

            var sut = Create<ProxyInvokerMiddleware>();

            // Act
            backend1.ConcurrencyCounter.Value.Should().Be(0);
            endpoint1.ConcurrencyCounter.Value.Should().Be(0);

            var task = sut.Invoke(httpContext);
            if (task.IsFaulted)
            {
                // Something went wrong, don't hang the test.
                await task;
            }
            await tcs1.Task; // Wait until we get to the proxying step.
            backend1.ConcurrencyCounter.Value.Should().Be(1);
            endpoint1.ConcurrencyCounter.Value.Should().Be(1);

            tcs2.TrySetResult(true);
            await task;
            backend1.ConcurrencyCounter.Value.Should().Be(0);
            endpoint1.ConcurrencyCounter.Value.Should().Be(0);

            // Assert
            Mock<IHttpProxy>().Verify();
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

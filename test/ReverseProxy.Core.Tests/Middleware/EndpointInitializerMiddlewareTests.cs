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
    public class EndpointInitializerMiddlewareTests : TestAutoMockBase
    {
        public EndpointInitializerMiddlewareTests()
        {
            Provide<IOperationLogger, TextOperationLogger>();
            Provide<RequestDelegate>(context => Task.CompletedTask);
        }

        [Fact]
        public void Constructor_Works()
        {
            Create<EndpointInitializerMiddleware>();
        }
        
        [Fact]
        public async Task Invoke_SetsFeatures()
        {
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

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
                route: new RouteInfo("route1"),
                matcherSummary: null,
                priority: null,
                backendOrNull: backend1,
                aspNetCoreEndpoints: aspNetCoreEndpoints.AsReadOnly());
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            var httpContext = new DefaultHttpContext();
            httpContext.SetEndpoint(aspNetCoreEndpoint);

            var sut = Create<EndpointInitializerMiddleware>();

            await sut.Invoke(httpContext);

            var feature = httpContext.Features.Get<IAvailableBackendEndpointsFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.Endpoints);
            Assert.Equal(1, feature.Endpoints.Count);
            Assert.Same(endpoint1, feature.Endpoints[0]);

            var backend = httpContext.Features.Get<BackendInfo>();
            Assert.Same(backend1, backend);

            Assert.Equal(200, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_NoHealthyEndpoints_503()
        {
            var proxyHttpClientFactoryMock = new Mock<IProxyHttpClientFactory>();
            var backend1 = new BackendInfo(
                backendId: "backend1",
                endpointManager: new EndpointManager(),
                proxyHttpClientFactory: proxyHttpClientFactoryMock.Object);
            backend1.Config.Value = new BackendConfig(
                new BackendConfig.BackendHealthCheckOptions(enabled: true, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan, 0, ""),
                new BackendConfig.BackendLoadBalancingOptions());
            var endpoint1 = backend1.EndpointManager.GetOrCreateItem(
                "endpoint1",
                endpoint =>
                {
                    endpoint.Config.Value = new EndpointConfig("https://localhost:123/a/b/");
                    endpoint.DynamicState.Value = new EndpointDynamicState(EndpointHealth.Unhealthy);
                });

            var aspNetCoreEndpoints = new List<Endpoint>();
            var routeConfig = new RouteConfig(
                route: new RouteInfo("route1"),
                matcherSummary: null,
                priority: null,
                backendOrNull: backend1,
                aspNetCoreEndpoints: aspNetCoreEndpoints.AsReadOnly());
            var aspNetCoreEndpoint = CreateAspNetCoreEndpoint(routeConfig);
            aspNetCoreEndpoints.Add(aspNetCoreEndpoint);
            var httpContext = new DefaultHttpContext();
            httpContext.SetEndpoint(aspNetCoreEndpoint);

            var sut = Create<EndpointInitializerMiddleware>();

            await sut.Invoke(httpContext);

            var feature = httpContext.Features.Get<IAvailableBackendEndpointsFeature>();
            Assert.Null(feature);

            var backend = httpContext.Features.Get<BackendInfo>();
            Assert.Null(backend);

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

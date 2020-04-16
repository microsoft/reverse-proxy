// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
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
    public class LoadBalancerMiddlewareTests : TestAutoMockBase
    {
        public LoadBalancerMiddlewareTests()
        {
            Provide<IOperationLogger, TextOperationLogger>();
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
            var endpoint2 = backend1.EndpointManager.GetOrCreateItem(
                "endpoint2",
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

            Mock<ILoadBalancer>()
                .Setup(l => l.PickEndpoint(It.IsAny<IReadOnlyList<EndpointInfo>>(),  It.IsAny<BackendConfig.BackendLoadBalancingOptions>()))
                .Returns(endpoint1);

            httpContext.Features.Set<IAvailableBackendEndpointsFeature>(
                new AvailableBackendEndpointsFeature() { Endpoints = new List<EndpointInfo>() { endpoint1, endpoint2 }.AsReadOnly() });
            httpContext.Features.Set(backend1);

            var sut = Create<LoadBalancingMiddleware>();

            await sut.Invoke(httpContext);

            var feature = httpContext.Features.Get<IAvailableBackendEndpointsFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.Endpoints);
            Assert.Equal(1, feature.Endpoints.Count);
            Assert.Same(endpoint1, feature.Endpoints[0]);

            Assert.Equal(200, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_ServiceReturnsNoResults_503()
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
            var endpoint2 = backend1.EndpointManager.GetOrCreateItem(
                "endpoint2",
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

            Mock<ILoadBalancer>()
                .Setup(l => l.PickEndpoint(It.IsAny<IReadOnlyList<EndpointInfo>>(), It.IsAny<BackendConfig.BackendLoadBalancingOptions>()))
                .Returns((EndpointInfo)null);

            httpContext.Features.Set<IAvailableBackendEndpointsFeature>(
                new AvailableBackendEndpointsFeature() { Endpoints = new List<EndpointInfo>() { endpoint1, endpoint2 }.AsReadOnly() });
            httpContext.Features.Set(backend1);

            var sut = Create<LoadBalancingMiddleware>();

            await sut.Invoke(httpContext);

            var feature = httpContext.Features.Get<IAvailableBackendEndpointsFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.Endpoints);
            Assert.Equal(2, feature.Endpoints.Count); // Unmodified

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

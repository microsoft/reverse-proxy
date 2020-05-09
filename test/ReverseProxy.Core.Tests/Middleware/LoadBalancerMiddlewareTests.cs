// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Common.Telemetry;
using Microsoft.ReverseProxy.Core.Abstractions;
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
            Provide<IOperationLogger<LoadBalancingMiddleware>, TextOperationLogger<LoadBalancingMiddleware>>();
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
                destinationManager: new DestinationManager(),
                proxyHttpClientFactory: proxyHttpClientFactoryMock.Object);
            backend1.Config.Value = new BackendConfig(default, new BackendConfig.BackendLoadBalancingOptions(LoadBalancingMode.RoundRobin));
            var destination1 = backend1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.Config.Value = new DestinationConfig("https://localhost:123/a/b/");
                    destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Healthy);
                });
            var destination2 = backend1.DestinationManager.GetOrCreateItem(
                "destination2",
                destination =>
                {
                    destination.Config.Value = new DestinationConfig("https://localhost:123/a/b/");
                    destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Healthy);
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
            Provide<ILoadBalancer, LoadBalancer>();

            httpContext.Features.Set<IAvailableDestinationsFeature>(
                new AvailableDestinationsFeature() { Destinations = new List<DestinationInfo>() { destination1, destination2 }.AsReadOnly() });
            httpContext.Features.Set(backend1);

            var sut = Create<LoadBalancingMiddleware>();

            await sut.Invoke(httpContext);

            var feature = httpContext.Features.Get<IAvailableDestinationsFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.Destinations);
            Assert.Equal(1, feature.Destinations.Count);
            Assert.Same(destination1, feature.Destinations[0]);

            Assert.Equal(200, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_ServiceReturnsNoResults_503()
        {
            var proxyHttpClientFactoryMock = new Mock<IProxyHttpClientFactory>();
            var backend1 = new BackendInfo(
                backendId: "backend1",
                destinationManager: new DestinationManager(),
                proxyHttpClientFactory: proxyHttpClientFactoryMock.Object);
            var destination1 = backend1.DestinationManager.GetOrCreateItem(
                "destination1",
                destination =>
                {
                    destination.Config.Value = new DestinationConfig("https://localhost:123/a/b/");
                    destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Healthy);
                });
            var destination2 = backend1.DestinationManager.GetOrCreateItem(
                "destination2",
                destination =>
                {
                    destination.Config.Value = new DestinationConfig("https://localhost:123/a/b/");
                    destination.DynamicState.Value = new DestinationDynamicState(DestinationHealth.Healthy);
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
                .Setup(l => l.PickDestination(It.IsAny<IReadOnlyList<DestinationInfo>>(), It.IsAny<BackendConfig.BackendLoadBalancingOptions>()))
                .Returns((DestinationInfo)null);

            httpContext.Features.Set<IAvailableDestinationsFeature>(
                new AvailableDestinationsFeature() { Destinations = new List<DestinationInfo>() { destination1, destination2 }.AsReadOnly() });
            httpContext.Features.Set(backend1);

            var sut = Create<LoadBalancingMiddleware>();

            await sut.Invoke(httpContext);

            var feature = httpContext.Features.Get<IAvailableDestinationsFeature>();
            Assert.NotNull(feature);
            Assert.NotNull(feature.Destinations);
            Assert.Equal(2, feature.Destinations.Count); // Unmodified

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

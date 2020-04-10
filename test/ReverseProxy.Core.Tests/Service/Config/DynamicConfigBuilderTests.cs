// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Core.Configuration.DependencyInjection;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Tests
{
    public class DynamicConfigBuilderTests
    {
        private const string TestAddress = "https://localhost:123/";

        private IDynamicConfigBuilder CreateConfigBuilder(IBackendsRepo backends, IRoutesRepo routes, Action<IReverseProxyBuilder> configProxy = null)
        {
            var servicesBuilder = new ServiceCollection();
            servicesBuilder.AddOptions();
            var proxyBuilder = servicesBuilder.AddReverseProxy();
            configProxy?.Invoke(proxyBuilder);
            servicesBuilder.AddSingleton(backends);
            servicesBuilder.AddSingleton(routes);
            servicesBuilder.AddSingleton<TestService>();
            var services = servicesBuilder.BuildServiceProvider();
            return services.GetRequiredService<IDynamicConfigBuilder>();
        }

        private class TestBackendsRepo : IBackendsRepo
        {
            public TestBackendsRepo() { }

            public TestBackendsRepo(IDictionary<string, Backend> backends) { Backends = backends; }

            public IDictionary<string, Backend>  Backends { get; set; }

            public Task<IDictionary<string, Backend>> GetBackendsAsync(CancellationToken cancellation) => Task.FromResult(Backends);

            public Task SetBackendsAsync(IDictionary<string, Backend> backends, CancellationToken cancellation) =>
                throw new NotImplementedException();
        }

        private class TestRoutesRepo : IRoutesRepo
        {
            public TestRoutesRepo() { }

            public TestRoutesRepo(IList<ProxyRoute> routes) { Routes = routes; }

            public IList<ProxyRoute> Routes { get; set; }

            public Task<IList<ProxyRoute>> GetRoutesAsync(CancellationToken cancellation) => Task.FromResult(Routes);

            public Task SetRoutesAsync(IList<ProxyRoute> routes, CancellationToken cancellation) =>
                throw new NotImplementedException();
        }

        private class TestService
        {
            public int CallCount { get; set; }
        }

        private TestBackendsRepo CreateOneBackend()
        {
            return new TestBackendsRepo(new Dictionary<string, Backend>
            {
                {
                    "backend1", new Backend
                    {
                        Endpoints =
                        {
                            { "ep1", new BackendEndpoint { Address = TestAddress } }
                        }
                    }
                }
            });
        }

        [Fact]
        public void Constructor_Works()
        {
            CreateConfigBuilder(new TestBackendsRepo(), new TestRoutesRepo());
        }

        [Fact]
        public async Task BuildConfigAsync_NullInput_Works()
        {
            var errorReporter = new TestConfigErrorReporter();
            var configBuilder = CreateConfigBuilder(new TestBackendsRepo(), new TestRoutesRepo());

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Backends);
            Assert.Empty(result.Value.Routes);
        }

        [Fact]
        public async Task BuildConfigAsync_EmptyInput_Works()
        {
            var errorReporter = new TestConfigErrorReporter();

            var configBuilder = CreateConfigBuilder(new TestBackendsRepo(new Dictionary<string, Backend>()), new TestRoutesRepo(new List<ProxyRoute>()));
            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Backends);
            Assert.Empty(result.Value.Routes);
        }

        [Fact]
        public async Task BuildConfigAsync_OneBackend_Works()
        {
            var errorReporter = new TestConfigErrorReporter();
            var configBuilder = CreateConfigBuilder(CreateOneBackend(), new TestRoutesRepo());

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result.Value);
            Assert.Single(result.Value.Backends);
            var backend = result.Value.Backends["backend1"];
            Assert.NotNull(backend);
            Assert.Single(backend.Endpoints);
            var endpoint = backend.Endpoints["ep1"];
            Assert.NotNull(endpoint);
            Assert.Equal(TestAddress, endpoint.Address);
        }

        [Fact]
        public async Task BuildConfigAsync_ValidRoute_Works()
        {
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, BackendId = "backend1" };
            var configBuilder = CreateConfigBuilder(new TestBackendsRepo(), new TestRoutesRepo(new[] { route1 }));

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Backends);
            Assert.Single(result.Value.Routes);
            Assert.Same(route1.RouteId, result.Value.Routes[0].RouteId);
        }

        [Fact]
        public async Task BuildConfigAsync_RouteValidationError_SkipsRoute()
        {
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "invalid host name" }, Priority = 1, BackendId = "backend1" };
            var configBuilder = CreateConfigBuilder(new TestBackendsRepo(), new TestRoutesRepo(new[] { route1 }));

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Backends);
            Assert.Empty(result.Value.Routes);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigureBackendActions_Works()
        {
            var errorReporter = new TestConfigErrorReporter();
            var configBuilder = CreateConfigBuilder(CreateOneBackend(), new TestRoutesRepo(),
                proxyBuilder =>
                {
                    proxyBuilder.ConfigureBackendDefaults((id, backend) =>
                    {
                        backend.HealthCheckOptions = new HealthCheckOptions() { Enabled = true, Interval = TimeSpan.FromSeconds(12) };
                    });
                    proxyBuilder.ConfigureBackend("backend1", backend => backend.HealthCheckOptions.Port = 13);
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result.Value);
            Assert.Single(result.Value.Backends);
            var backend = result.Value.Backends["backend1"];
            Assert.NotNull(backend);
            Assert.True(backend.HealthCheckOptions.Enabled);
            Assert.Equal(TimeSpan.FromSeconds(12), backend.HealthCheckOptions.Interval);
            Assert.Equal(13, backend.HealthCheckOptions.Port);
            Assert.Single(backend.Endpoints);
            var endpoint = backend.Endpoints["ep1"];
            Assert.NotNull(endpoint);
            Assert.Equal(TestAddress, endpoint.Address);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigureBackendActionsWithServices_Works()
        {
            var errorReporter = new TestConfigErrorReporter();
            TestService testServiceInstance = null;
            var configBuilder = CreateConfigBuilder(CreateOneBackend(), new TestRoutesRepo(),
                proxyBuilder =>
                {
                    proxyBuilder.ConfigureBackendDefaults<TestService>((id, backend, testService) =>
                    {
                        testService.CallCount++;
                        testServiceInstance = testService;
                        backend.HealthCheckOptions = new HealthCheckOptions() { Enabled = true, Interval = TimeSpan.FromSeconds(12) };
                    });
                    proxyBuilder.ConfigureBackend<TestService>("backend1", (backend, testService) =>
                    {
                        testService.CallCount++;
                        backend.HealthCheckOptions.Port = 13;
                    });
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result.Value);
            Assert.Single(result.Value.Backends);
            var backend = result.Value.Backends["backend1"];
            Assert.NotNull(backend);
            Assert.True(backend.HealthCheckOptions.Enabled);
            Assert.Equal(TimeSpan.FromSeconds(12), backend.HealthCheckOptions.Interval);
            Assert.Equal(13, backend.HealthCheckOptions.Port);
            Assert.Single(backend.Endpoints);
            var endpoint = backend.Endpoints["ep1"];
            Assert.NotNull(endpoint);
            Assert.Equal(TestAddress, endpoint.Address);
            Assert.Equal(2, testServiceInstance.CallCount);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigureBackendDefaultsActionThrows_BackendSkipped()
        {
            var errorReporter = new TestConfigErrorReporter();
            var configBuilder = CreateConfigBuilder(CreateOneBackend(), new TestRoutesRepo(),
                proxyBuilder =>
                {
                    proxyBuilder.ConfigureBackendDefaults((id, backend) => throw new NotFiniteNumberException("Test exception"));
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Backends);
            Assert.NotEmpty(errorReporter.Errors);
            Assert.IsType<NotFiniteNumberException>(errorReporter.Errors.Single().Exception);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigureBackendActionThrows_BackendSkipped()
        {
            var errorReporter = new TestConfigErrorReporter();
            var configBuilder = CreateConfigBuilder(CreateOneBackend(), new TestRoutesRepo(),
                proxyBuilder =>
                {
                    proxyBuilder.ConfigureBackend("backend1", backend => throw new NotFiniteNumberException("Test exception"));
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Backends);
            Assert.NotEmpty(errorReporter.Errors);
            Assert.IsType<NotFiniteNumberException>(errorReporter.Errors.Single().Exception);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigureRouteActions_Works()
        {
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, BackendId = "backend1" };
            var configBuilder = CreateConfigBuilder(new TestBackendsRepo(), new TestRoutesRepo(new[] { route1 }),
                proxyBuilder =>
                {
                    proxyBuilder.ConfigureRouteDefaults(route => route.Priority = 12);
                    proxyBuilder.ConfigureRoute("route1", route => route.Match.Path = "/CustomPath");
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Backends);
            Assert.Single(result.Value.Routes);
            Assert.Same(route1.RouteId, result.Value.Routes[0].RouteId);
            Assert.Equal(12, route1.Priority);
            Assert.Equal("/CustomPath", route1.Match.Path);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigureRouteActionsWithServices_Works()
        {
            var errorReporter = new TestConfigErrorReporter();
            TestService testServiceInstance = null;
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, BackendId = "backend1" };
            var route2 = new ProxyRoute { RouteId = "route2", Match = { Host = "example.com" }, Priority = 1, BackendId = "backend2" };
            var configBuilder = CreateConfigBuilder(new TestBackendsRepo(), new TestRoutesRepo(new[] { route1, route2 }),
                proxyBuilder =>
                {
                    proxyBuilder.ConfigureRouteDefaults<TestService>((route, service) =>
                    {
                        service.CallCount++;
                        testServiceInstance = service;
                        route.Priority = 12;
                    });
                    proxyBuilder.ConfigureRoute<TestService>("route1", (route, service) =>
                    {
                        service.CallCount++;
                        route.Match.Path = "/CustomPath";
                    });
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Backends);
            Assert.Equal(2, result.Value.Routes.Count);
            Assert.Same(route1.RouteId, result.Value.Routes[0].RouteId);
            Assert.Same(route2.RouteId, result.Value.Routes[1].RouteId);
            Assert.Equal(12, route1.Priority);
            Assert.Equal(12, route2.Priority);
            Assert.Equal("/CustomPath", route1.Match.Path);
            Assert.Null(route2.Match.Path);
            Assert.Equal(3, testServiceInstance.CallCount);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigureRouteDefaultsActionThrows_SkipsRoute()
        {
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, BackendId = "backend1" };
            var route2 = new ProxyRoute { RouteId = "route2", Match = { Host = "example2.com" }, Priority = 1, BackendId = "backend2" };
            var configBuilder = CreateConfigBuilder(new TestBackendsRepo(), new TestRoutesRepo(new[] { route1, route2 }),
                proxyBuilder =>
                {
                    proxyBuilder.ConfigureRouteDefaults(route => throw new NotFiniteNumberException("Test exception"));
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Backends);
            Assert.Empty(result.Value.Routes);
            Assert.Equal(2, errorReporter.Errors.Count);
            Assert.IsType<NotFiniteNumberException>(errorReporter.Errors.First().Exception);
            Assert.IsType<NotFiniteNumberException>(errorReporter.Errors.Skip(1).First().Exception);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigureRouteActionThrows_SkipsRoute()
        {
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, BackendId = "backend1" };
            var route2 = new ProxyRoute { RouteId = "route2", Match = { Host = "example2.com" }, Priority = 1, BackendId = "backend2" };
            var configBuilder = CreateConfigBuilder(new TestBackendsRepo(), new TestRoutesRepo(new[] { route1, route2 }),
                proxyBuilder =>
                {
                    proxyBuilder.ConfigureRoute("route1", route => throw new NotFiniteNumberException("Test exception"));
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Backends);
            Assert.Single(result.Value.Routes);
            Assert.Same(route2.RouteId, result.Value.Routes[0].RouteId);
            Assert.IsType<NotFiniteNumberException>(errorReporter.Errors.Single().Exception);
        }
    }
}

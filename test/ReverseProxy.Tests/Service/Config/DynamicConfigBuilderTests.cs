// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Configuration.DependencyInjection;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Tests
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
            servicesBuilder.AddDataProtection();
            servicesBuilder.AddLogging();
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
                        Id = "backend1",
                        Destinations =
                        {
                            { "d1", new Destination { Address = TestAddress } }
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
            Assert.Equal("backend1", backend.Id);
            Assert.Single(backend.Destinations);
            var destination = backend.Destinations["d1"];
            Assert.NotNull(destination);
            Assert.Equal(TestAddress, destination.Address);
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
        public async Task BuildConfigAsync_ConfigFilterRouteActions_CanFixBrokenRoute()
        {
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "invalid host name" }, Priority = 1, BackendId = "backend1" };
            var configBuilder = CreateConfigBuilder(new TestBackendsRepo(), new TestRoutesRepo(new[] { route1 }),
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<FixRouteHostFilter>();
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Backends);
            Assert.Single(result.Value.Routes);
            var builtRoute = result.Value.Routes[0];
            Assert.Same(route1.RouteId, builtRoute.RouteId);
            Assert.Equal("example.com", builtRoute.Host);
        }

        private class FixRouteHostFilter : IProxyConfigFilter
        {
            public Task ConfigureBackendAsync(Backend backend, CancellationToken cancel)
            {
                return Task.CompletedTask;
            }

            public Task ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel)
            {
                route.Match.Host = "example.com";
                return Task.CompletedTask;
            }
        }

        private class BackendAndRouteFilter : IProxyConfigFilter
        {
            public Task ConfigureBackendAsync(Backend backend, CancellationToken cancel)
            {
                backend.HealthCheckOptions = new HealthCheckOptions() { Enabled = true, Interval = TimeSpan.FromSeconds(12) };
                return Task.CompletedTask;
            }

            public Task ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel)
            {
                route.Priority = 12;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigFilterConfiguresBackend_Works()
        {
            var errorReporter = new TestConfigErrorReporter();
            var configBuilder = CreateConfigBuilder(CreateOneBackend(), new TestRoutesRepo(),
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<BackendAndRouteFilter>();
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
            Assert.Single(backend.Destinations);
            var destination = backend.Destinations["d1"];
            Assert.NotNull(destination);
            Assert.Equal(TestAddress, destination.Address);
        }

        private class BackendAndRouteThrows : IProxyConfigFilter
        {
            public Task ConfigureBackendAsync(Backend backend, CancellationToken cancel)
            {
                throw new NotFiniteNumberException("Test exception");
            }

            public Task ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel)
            {
                throw new NotFiniteNumberException("Test exception");
            }
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigFilterBackendActionThrows_BackendSkipped()
        {
            var errorReporter = new TestConfigErrorReporter();
            var configBuilder = CreateConfigBuilder(CreateOneBackend(), new TestRoutesRepo(),
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<BackendAndRouteThrows>();
                    proxyBuilder.AddProxyConfigFilter<BackendAndRouteThrows>();
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
        public async Task BuildConfigAsync_ConfigFilterRouteActions_Works()
        {
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, BackendId = "backend1" };
            var configBuilder = CreateConfigBuilder(new TestBackendsRepo(), new TestRoutesRepo(new[] { route1 }),
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<BackendAndRouteFilter>();
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value.Backends);
            Assert.Single(result.Value.Routes);
            Assert.Same(route1.RouteId, result.Value.Routes[0].RouteId);
            Assert.Equal(12, route1.Priority);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigFilterRouteActionThrows_SkipsRoute()
        {
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, BackendId = "backend1" };
            var route2 = new ProxyRoute { RouteId = "route2", Match = { Host = "example2.com" }, Priority = 1, BackendId = "backend2" };
            var configBuilder = CreateConfigBuilder(new TestBackendsRepo(), new TestRoutesRepo(new[] { route1, route2 }),
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<BackendAndRouteThrows>();
                    proxyBuilder.AddProxyConfigFilter<BackendAndRouteThrows>();
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
    }
}

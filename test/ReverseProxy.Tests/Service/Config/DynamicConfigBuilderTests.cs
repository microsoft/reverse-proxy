// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Common;
using Microsoft.ReverseProxy.Configuration.DependencyInjection;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Tests
{
    public class DynamicConfigBuilderTests
    {
        private const string TestAddress = "https://localhost:123/";
        private readonly IReadOnlyList<ProxyRoute> EmptyRoutes = new List<ProxyRoute>();
        private readonly IReadOnlyList<Cluster> EmptyClusters = new List<Cluster>();

        private IDynamicConfigBuilder CreateConfigBuilder(ILoggerFactory loggerFactory, Action<IReverseProxyBuilder> configProxy = null)
        {
            var servicesBuilder = new ServiceCollection();
            servicesBuilder.AddOptions();
            var proxyBuilder = servicesBuilder.AddReverseProxy();
            configProxy?.Invoke(proxyBuilder);
            servicesBuilder.AddSingleton<TestService>();
            servicesBuilder.AddDataProtection();
            servicesBuilder.AddSingleton(loggerFactory);
            servicesBuilder.AddLogging();
            servicesBuilder.AddRouting();
            var services = servicesBuilder.BuildServiceProvider();
            return services.GetRequiredService<IDynamicConfigBuilder>();
        }

        private class TestService
        {
            public int CallCount { get; set; }
        }

        private IReadOnlyList<Cluster> CreateOneCluster()
        {
            return new List<Cluster>
            {
                new Cluster
                {
                    Id = "cluster1",
                    Destinations =
                    {
                        { "d1", new Destination { Address = TestAddress } }
                    }
                }
            };
        }

        [Fact]
        public void Constructor_Works()
        {
            CreateConfigBuilder(NullLoggerFactory.Instance);
        }

        [Fact]
        public async Task BuildConfigAsync_NullInput_Throws()
        {
            var factory = new TestLoggerFactory();
            var configBuilder = CreateConfigBuilder(factory);

            await Assert.ThrowsAsync<ArgumentNullException>(() => configBuilder.BuildConfigAsync(null, EmptyClusters, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(() => configBuilder.BuildConfigAsync(EmptyRoutes, null, CancellationToken.None));
        }

        [Fact]
        public async Task BuildConfigAsync_EmptyInput_Works()
        {
            var factory = new TestLoggerFactory();
            var configBuilder = CreateConfigBuilder(factory);
            var result = await configBuilder.BuildConfigAsync(EmptyRoutes, EmptyClusters, CancellationToken.None);

            Assert.Empty(factory.Logger.Errors);
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Empty(result.Routes);
        }

        [Fact]
        public async Task BuildConfigAsync_OneCluster_Works()
        {
            var factory = new TestLoggerFactory();
            var configBuilder = CreateConfigBuilder(factory);

            var result = await configBuilder.BuildConfigAsync(EmptyRoutes, CreateOneCluster(), CancellationToken.None);

            // Assert
            Assert.Empty(factory.Logger.Errors);
            Assert.NotNull(result);
            Assert.Single(result.Clusters);
            var cluster = result.Clusters["cluster1"];
            Assert.NotNull(cluster);
            Assert.Equal("cluster1", cluster.Id);
            Assert.Single(cluster.Destinations);
            var destination = cluster.Destinations["d1"];
            Assert.NotNull(destination);
            Assert.Equal(TestAddress, destination.Address);
        }

        [Fact]
        public async Task BuildConfigAsync_ValidRoute_Works()
        {
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Hosts = new[] { "example.com" } }, Priority = 1, ClusterId = "cluster1" };
            var factory = new TestLoggerFactory();
            var configBuilder = CreateConfigBuilder(factory);

            var result = await configBuilder.BuildConfigAsync(new[] { route1 }, EmptyClusters,  CancellationToken.None);

            Assert.Empty(factory.Logger.Errors);
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Single(result.Routes);
            Assert.Same(route1.RouteId, result.Routes[0].RouteId);
        }

        [Fact]
        public async Task BuildConfigAsync_RouteValidationError_SkipsRoute()
        {
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Hosts = new[] { "invalid host name" } }, Priority = 1, ClusterId = "cluster1" };
            var configBuilder = CreateConfigBuilder(NullLoggerFactory.Instance);

            var result = await configBuilder.BuildConfigAsync(new[] { route1 }, EmptyClusters, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Empty(result.Routes);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigFilterRouteActions_CanFixBrokenRoute()
        {
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Hosts = new[] { "invalid host name" } }, Priority = 1, ClusterId = "cluster1" };
            var factory = new TestLoggerFactory();
            var configBuilder = CreateConfigBuilder(factory,
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<FixRouteHostFilter>();
                });

            var result = await configBuilder.BuildConfigAsync(new[] { route1 }, EmptyClusters, CancellationToken.None);

            Assert.Empty(factory.Logger.Errors);
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Single(result.Routes);
            var builtRoute = result.Routes[0];
            Assert.Same(route1.RouteId, builtRoute.RouteId);
            var host = Assert.Single(builtRoute.Match.Hosts);
            Assert.Equal("example.com", host);
        }

        private class FixRouteHostFilter : IProxyConfigFilter
        {
            public Task ConfigureClusterAsync(Cluster cluster, CancellationToken cancel)
            {
                return Task.CompletedTask;
            }

            public Task ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel)
            {
                route.Match.Hosts = new[] { "example.com" };
                return Task.CompletedTask;
            }
        }

        private class ClusterAndRouteFilter : IProxyConfigFilter
        {
            public Task ConfigureClusterAsync(Cluster cluster, CancellationToken cancel)
            {
                cluster.HealthCheckOptions = new HealthCheckOptions() { Enabled = true, Interval = TimeSpan.FromSeconds(12) };
                return Task.CompletedTask;
            }

            public Task ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel)
            {
                route.Priority = 12;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigFilterConfiguresCluster_Works()
        {
            var factory = new TestLoggerFactory();
            var configBuilder = CreateConfigBuilder(factory,
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<ClusterAndRouteFilter>();
                });

            var result = await configBuilder.BuildConfigAsync(EmptyRoutes, CreateOneCluster(), CancellationToken.None);

            Assert.Empty(factory.Logger.Errors);
            Assert.NotNull(result);
            Assert.Single(result.Clusters);
            var cluster = result.Clusters["cluster1"];
            Assert.NotNull(cluster);
            Assert.True(cluster.HealthCheckOptions.Enabled);
            Assert.Equal(TimeSpan.FromSeconds(12), cluster.HealthCheckOptions.Interval);
            Assert.Single(cluster.Destinations);
            var destination = cluster.Destinations["d1"];
            Assert.NotNull(destination);
            Assert.Equal(TestAddress, destination.Address);
        }

        private class ClusterAndRouteThrows : IProxyConfigFilter
        {
            public Task ConfigureClusterAsync(Cluster cluster, CancellationToken cancel)
            {
                throw new NotFiniteNumberException("Test exception");
            }

            public Task ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel)
            {
                throw new NotFiniteNumberException("Test exception");
            }
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigFilterClusterActionThrows_ClusterSkipped()
        {
            var factory = new TestLoggerFactory();
            var configBuilder = CreateConfigBuilder(factory,
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<ClusterAndRouteThrows>();
                    proxyBuilder.AddProxyConfigFilter<ClusterAndRouteThrows>();
                });

            var result = await configBuilder.BuildConfigAsync(EmptyRoutes, CreateOneCluster(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.NotEmpty(factory.Logger.Errors);
            Assert.IsType<NotFiniteNumberException>(factory.Logger.Errors.Single().exception);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigFilterRouteActions_Works()
        {
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Hosts = new[] { "example.com" } }, Priority = 1, ClusterId = "cluster1" };
            var factory = new TestLoggerFactory();
            var configBuilder = CreateConfigBuilder(factory,
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<ClusterAndRouteFilter>();
                });

            var result = await configBuilder.BuildConfigAsync(new[] { route1 }, EmptyClusters, CancellationToken.None);

            Assert.Empty(factory.Logger.Errors);
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Single(result.Routes);
            Assert.Same(route1.RouteId, result.Routes[0].RouteId);
            Assert.Equal(12, result.Routes[0].Priority);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigFilterRouteActionThrows_SkipsRoute()
        {
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Hosts = new[] { "example.com" } }, Priority = 1, ClusterId = "cluster1" };
            var route2 = new ProxyRoute { RouteId = "route2", Match = { Hosts = new[] { "example2.com" } }, Priority = 1, ClusterId = "cluster2" };
            var factory = new TestLoggerFactory();
            var configBuilder = CreateConfigBuilder(factory,
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<ClusterAndRouteThrows>();
                    proxyBuilder.AddProxyConfigFilter<ClusterAndRouteThrows>();
                });

            var result = await configBuilder.BuildConfigAsync(new[] { route1, route2 }, EmptyClusters, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Empty(result.Routes);
            Assert.Equal(2, factory.Logger.Errors.Count());
            Assert.IsType<NotFiniteNumberException>(factory.Logger.Errors.First().exception);
            Assert.IsType<NotFiniteNumberException>(factory.Logger.Errors.Skip(1).First().exception);
        }
    }
}

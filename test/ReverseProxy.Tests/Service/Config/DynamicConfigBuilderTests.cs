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

        private IDynamicConfigBuilder CreateConfigBuilder(IClustersRepo clusters, IRoutesRepo routes, Action<IReverseProxyBuilder> configProxy = null)
        {
            var servicesBuilder = new ServiceCollection();
            servicesBuilder.AddOptions();
            servicesBuilder.AddAuthorization();
            var proxyBuilder = servicesBuilder.AddReverseProxy();
            configProxy?.Invoke(proxyBuilder);
            servicesBuilder.AddSingleton(clusters);
            servicesBuilder.AddSingleton(routes);
            servicesBuilder.AddSingleton<TestService>();
            servicesBuilder.AddDataProtection();
            servicesBuilder.AddLogging();
            servicesBuilder.AddRouting();
            var services = servicesBuilder.BuildServiceProvider();
            return services.GetRequiredService<IDynamicConfigBuilder>();
        }

        private class TestClustersRepo : IClustersRepo
        {
            public TestClustersRepo() { }

            public TestClustersRepo(IDictionary<string, Cluster> clusters) { Clusters = clusters; }

            public IDictionary<string, Cluster>  Clusters { get; set; }

            public Task<IDictionary<string, Cluster>> GetClustersAsync(CancellationToken cancellation) => Task.FromResult(Clusters);

            public Task SetClustersAsync(IDictionary<string, Cluster> clusters, CancellationToken cancellation) =>
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

        private TestClustersRepo CreateOneCluster()
        {
            return new TestClustersRepo(new Dictionary<string, Cluster>
            {
                {
                    "cluster1", new Cluster
                    {
                        Id = "cluster1",
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
            CreateConfigBuilder(new TestClustersRepo(), new TestRoutesRepo());
        }

        [Fact]
        public async Task BuildConfigAsync_NullInput_Works()
        {
            var errorReporter = new TestConfigErrorReporter();
            var configBuilder = CreateConfigBuilder(new TestClustersRepo(), new TestRoutesRepo());

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Empty(result.Routes);
        }

        [Fact]
        public async Task BuildConfigAsync_EmptyInput_Works()
        {
            var errorReporter = new TestConfigErrorReporter();

            var configBuilder = CreateConfigBuilder(new TestClustersRepo(new Dictionary<string, Cluster>()), new TestRoutesRepo(new List<ProxyRoute>()));
            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Empty(result.Routes);
        }

        [Fact]
        public async Task BuildConfigAsync_OneCluster_Works()
        {
            var errorReporter = new TestConfigErrorReporter();
            var configBuilder = CreateConfigBuilder(CreateOneCluster(), new TestRoutesRepo());

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            Assert.Empty(errorReporter.Errors);
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
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, ClusterId = "cluster1" };
            var configBuilder = CreateConfigBuilder(new TestClustersRepo(), new TestRoutesRepo(new[] { route1 }));

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Single(result.Routes);
            Assert.Same(route1.RouteId, result.Routes[0].RouteId);
        }

        [Fact]
        public async Task BuildConfigAsync_RouteValidationError_SkipsRoute()
        {
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "invalid host name" }, Priority = 1, ClusterId = "cluster1" };
            var configBuilder = CreateConfigBuilder(new TestClustersRepo(), new TestRoutesRepo(new[] { route1 }));

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Empty(result.Routes);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigFilterRouteActions_CanFixBrokenRoute()
        {
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "invalid host name" }, Priority = 1, ClusterId = "cluster1" };
            var configBuilder = CreateConfigBuilder(new TestClustersRepo(), new TestRoutesRepo(new[] { route1 }),
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<FixRouteHostFilter>();
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Single(result.Routes);
            var builtRoute = result.Routes[0];
            Assert.Same(route1.RouteId, builtRoute.RouteId);
            Assert.Equal("example.com", builtRoute.Host);
        }

        private class FixRouteHostFilter : IProxyConfigFilter
        {
            public Task ConfigureClusterAsync(Cluster cluster, CancellationToken cancel)
            {
                return Task.CompletedTask;
            }

            public Task ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel)
            {
                route.Match.Host = "example.com";
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
            var errorReporter = new TestConfigErrorReporter();
            var configBuilder = CreateConfigBuilder(CreateOneCluster(), new TestRoutesRepo(),
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<ClusterAndRouteFilter>();
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.Empty(errorReporter.Errors);
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
            var errorReporter = new TestConfigErrorReporter();
            var configBuilder = CreateConfigBuilder(CreateOneCluster(), new TestRoutesRepo(),
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<ClusterAndRouteThrows>();
                    proxyBuilder.AddProxyConfigFilter<ClusterAndRouteThrows>();
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.NotEmpty(errorReporter.Errors);
            Assert.IsType<NotFiniteNumberException>(errorReporter.Errors.Single().Exception);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigFilterRouteActions_Works()
        {
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, ClusterId = "cluster1" };
            var configBuilder = CreateConfigBuilder(new TestClustersRepo(), new TestRoutesRepo(new[] { route1 }),
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<ClusterAndRouteFilter>();
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            Assert.Empty(errorReporter.Errors);
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Single(result.Routes);
            Assert.Same(route1.RouteId, result.Routes[0].RouteId);
            Assert.Equal(12, route1.Priority);
        }

        [Fact]
        public async Task BuildConfigAsync_ConfigFilterRouteActionThrows_SkipsRoute()
        {
            var errorReporter = new TestConfigErrorReporter();
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Host = "example.com" }, Priority = 1, ClusterId = "cluster1" };
            var route2 = new ProxyRoute { RouteId = "route2", Match = { Host = "example2.com" }, Priority = 1, ClusterId = "cluster2" };
            var configBuilder = CreateConfigBuilder(new TestClustersRepo(), new TestRoutesRepo(new[] { route1, route2 }),
                proxyBuilder =>
                {
                    proxyBuilder.AddProxyConfigFilter<ClusterAndRouteThrows>();
                    proxyBuilder.AddProxyConfigFilter<ClusterAndRouteThrows>();
                });

            var result = await configBuilder.BuildConfigAsync(errorReporter, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Clusters);
            Assert.Empty(result.Routes);
            Assert.Equal(2, errorReporter.Errors.Count);
            Assert.IsType<NotFiniteNumberException>(errorReporter.Errors.First().Exception);
            Assert.IsType<NotFiniteNumberException>(errorReporter.Errors.Skip(1).First().Exception);
        }
    }
}

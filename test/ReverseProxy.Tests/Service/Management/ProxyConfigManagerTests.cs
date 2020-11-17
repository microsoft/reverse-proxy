// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Configuration;
using Microsoft.ReverseProxy.Service.HealthChecks;
using Microsoft.ReverseProxy.Utilities;
using Microsoft.ReverseProxy.Utilities.Tests;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Management.Tests
{
    public class ProxyConfigManagerTests
    {
        private IServiceProvider CreateServices(List<ProxyRoute> routes, List<Cluster> clusters, Action<IReverseProxyBuilder> configureProxy = null)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddRouting();
            var proxyBuilder = serviceCollection.AddReverseProxy().LoadFromMemory(routes, clusters);
            serviceCollection.TryAddSingleton(new Mock<IWebHostEnvironment>().Object);
            var activeHealthPolicy = new Mock<IActiveHealthCheckPolicy>();
            activeHealthPolicy.SetupGet(p => p.Name).Returns("activePolicyA");
            serviceCollection.AddSingleton(activeHealthPolicy.Object);
            configureProxy?.Invoke(proxyBuilder);
            var services = serviceCollection.BuildServiceProvider();
            var routeBuilder = services.GetRequiredService<ProxyEndpointFactory>();
            routeBuilder.SetProxyPipeline(context => Task.CompletedTask);
            return services;
        }

        [Fact]
        public void Constructor_Works()
        {
            var services = CreateServices(new List<ProxyRoute>(), new List<Cluster>());
            _ = services.GetRequiredService<ProxyConfigManager>();
        }

        [Fact]
        public async Task NullRoutes_StartsEmpty()
        {
            var services = CreateServices(null, new List<Cluster>());
            var manager = services.GetRequiredService<ProxyConfigManager>();
            var dataSource = await manager.InitialLoadAsync();
            Assert.NotNull(dataSource);
            var endpoints = dataSource.Endpoints;
            Assert.Empty(endpoints);
        }

        [Fact]
        public async Task NullClusters_StartsEmpty()
        {
            var services = CreateServices(new List<ProxyRoute>(), null);
            var manager = services.GetRequiredService<ProxyConfigManager>();
            var dataSource = await manager.InitialLoadAsync();
            Assert.NotNull(dataSource);
            var endpoints = dataSource.Endpoints;
            Assert.Empty(endpoints);
        }

        [Fact]
        public async Task Endpoints_StartsEmpty()
        {
            var services = CreateServices(new List<ProxyRoute>(), new List<Cluster>());
            var manager = services.GetRequiredService<ProxyConfigManager>();
            var dataSource = await manager.InitialLoadAsync();
            Assert.NotNull(dataSource);
            var endpoints = dataSource.Endpoints;
            Assert.Empty(endpoints);
        }

        [Fact]
        public async Task GetChangeToken_InitialValue()
        {
            var services = CreateServices(new List<ProxyRoute>(), new List<Cluster>());
            var manager = services.GetRequiredService<ProxyConfigManager>();
            var dataSource = await manager.InitialLoadAsync();
            Assert.NotNull(dataSource);
            var changeToken = dataSource.GetChangeToken();
            Assert.NotNull(changeToken);
            Assert.True(changeToken.ActiveChangeCallbacks);
            Assert.False(changeToken.HasChanged);
        }

        [Fact]
        public async Task BuildConfig_OneClusterOneDestinationOneRoute_Works()
        {
            const string TestAddress = "https://localhost:123/";

            var cluster = new Cluster
            {
                Id = "cluster1",
                Destinations = {
                    { "d1", new Destination { Address = TestAddress } }
                }
            };
            var route = new ProxyRoute
            {
                RouteId = "route1",
                ClusterId = "cluster1",
                Match = { Path = "/" }
            };

            var services = CreateServices(new List<ProxyRoute>() { route }, new List<Cluster>() { cluster });

            var manager = services.GetRequiredService<ProxyConfigManager>();
            var dataSource = await manager.InitialLoadAsync();

            Assert.NotNull(dataSource);
            var endpoints = dataSource.Endpoints;
            Assert.Single(endpoints);

            var clusterManager = services.GetRequiredService<IClusterManager>();
            var actualClusters = clusterManager.GetItems();
            Assert.Single(actualClusters);
            Assert.Equal("cluster1", actualClusters[0].ClusterId);
            Assert.NotNull(actualClusters[0].DestinationManager);
            Assert.NotNull(actualClusters[0].Config);
            Assert.NotNull(actualClusters[0].Config.HttpClient);

            var actualDestinations = actualClusters[0].DestinationManager.GetItems();
            Assert.Single(actualDestinations);
            Assert.Equal("d1", actualDestinations[0].DestinationId);
            Assert.NotNull(actualDestinations[0].Config);
            Assert.Equal(TestAddress, actualDestinations[0].Config.Address);

            var routeManager = services.GetRequiredService<IRouteManager>();
            var actualRoutes = routeManager.GetItems();
            Assert.Single(actualRoutes);
            Assert.Equal("route1", actualRoutes[0].RouteId);
            Assert.NotNull(actualRoutes[0].Config);
            Assert.Same(actualClusters[0], actualRoutes[0].Config.Cluster);
        }

        [Fact]
        public async Task InitialLoadAsync_ProxyHttpClientOptionsSet_CreateAndSetHttpClient()
        {
            const string TestAddress = "https://localhost:123/";

            var clientCertificate = TestResources.GetTestCertificate();
            var cluster = new Cluster
            {
                Id = "cluster1",
                Destinations = { { "d1", new Destination { Address = TestAddress } } },
                HttpClient = new ProxyHttpClientOptions {
                    SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                    MaxConnectionsPerServer = 10,
                    ClientCertificate = clientCertificate
                }
            };
            var route = new ProxyRoute
            {
                RouteId = "route1",
                ClusterId = "cluster1",
                Match = { Path = "/" }
            };

            var services = CreateServices(new List<ProxyRoute>() { route }, new List<Cluster>() { cluster });

            var manager = services.GetRequiredService<ProxyConfigManager>();
            var dataSource = await manager.InitialLoadAsync();

            Assert.NotNull(dataSource);

            var clusterManager = services.GetRequiredService<IClusterManager>();
            var actualClusters = clusterManager.GetItems();
            Assert.Single(actualClusters);
            Assert.Equal("cluster1", actualClusters[0].ClusterId);
            var clusterConfig = actualClusters[0].Config;
            Assert.NotNull(clusterConfig.HttpClient);
            Assert.Equal(SslProtocols.Tls11 | SslProtocols.Tls12, clusterConfig.HttpClientOptions.SslProtocols);
            Assert.Equal(10, clusterConfig.HttpClientOptions.MaxConnectionsPerServer);
            Assert.Same(clientCertificate, clusterConfig.HttpClientOptions.ClientCertificate);

            var handler = Proxy.Tests.ProxyHttpClientFactoryTests.GetHandler(clusterConfig.HttpClient);
            Assert.Equal(SslProtocols.Tls11 | SslProtocols.Tls12, handler.SslOptions.EnabledSslProtocols);
            Assert.Equal(10, handler.MaxConnectionsPerServer);
            Assert.Single(handler.SslOptions.ClientCertificates, clientCertificate);
        }

        [Fact]
        public async Task GetChangeToken_SignalsChange()
        {
            var services = CreateServices(new List<ProxyRoute>(), new List<Cluster>());
            var inMemoryConfig = (InMemoryConfigProvider)services.GetRequiredService<IProxyConfigProvider>();
            var configManager = services.GetRequiredService<ProxyConfigManager>();
            var dataSource = await configManager.InitialLoadAsync();

            var signaled1 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var signaled2 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            IReadOnlyList<AspNetCore.Http.Endpoint> readEndpoints1 = null;
            IReadOnlyList<AspNetCore.Http.Endpoint> readEndpoints2 = null;

            var changeToken1 = dataSource.GetChangeToken();
            changeToken1.RegisterChangeCallback(
                _ =>
                {
                    readEndpoints1 = dataSource.Endpoints;
                    signaled1.SetResult(1);
                }, null);

            // updating should signal the current change token
            Assert.False(signaled1.Task.IsCompleted);
            inMemoryConfig.Update(new List<ProxyRoute>() { new ProxyRoute() { RouteId = "r1", Match = { Path = "/" } } }, new List<Cluster>());
            await signaled1.Task.DefaultTimeout();

            var changeToken2 = dataSource.GetChangeToken();
            changeToken2.RegisterChangeCallback(
                _ =>
                {
                    readEndpoints2 = dataSource.Endpoints;
                    signaled2.SetResult(1);
                }, null);

            // updating again should only signal the new change token
            Assert.False(signaled2.Task.IsCompleted);
            inMemoryConfig.Update(new List<ProxyRoute>() { new ProxyRoute() { RouteId = "r2", Match = { Path = "/" } } }, new List<Cluster>());
            await signaled2.Task.DefaultTimeout();

            Assert.NotNull(readEndpoints1);
            Assert.NotNull(readEndpoints2);
        }

        [Fact]
        public async Task LoadAsync_RequestVersionValidationError_Throws()
        {
            const string TestAddress = "https://localhost:123/";

            var cluster = new Cluster
            {
                Id = "cluster1",
                Destinations = { { "d1", new Destination { Address = TestAddress } } },
                HttpRequest = new ProxyHttpRequestOptions() { Version = new Version(1, 2) }
            };

            var services = CreateServices(new List<ProxyRoute>(), new List<Cluster>() { cluster });
            var configManager = services.GetRequiredService<ProxyConfigManager>();

            var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
            Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);
            var agex = Assert.IsType<AggregateException>(ioEx.InnerException);

            Assert.Single(agex.InnerExceptions);
            var argex = Assert.IsType<ArgumentException>(agex.InnerExceptions.First());
            Assert.StartsWith("Outgoing request version", argex.Message);
        }

        [Fact]
        public async Task LoadAsync_RouteValidationError_Throws()
        {
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Hosts = new[] { "invalid host name" } }, ClusterId = "cluster1" };
            var services = CreateServices(new List<ProxyRoute>() { route1 }, new List<Cluster>());
            var configManager = services.GetRequiredService<ProxyConfigManager>();

            var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
            Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);
            var agex = Assert.IsType<AggregateException>(ioEx.InnerException);

            Assert.Single(agex.InnerExceptions);
            var argex = Assert.IsType<ArgumentException>(agex.InnerExceptions.First());
            Assert.StartsWith("Invalid host", argex.Message);
        }

        [Fact]
        public async Task LoadAsync_ConfigFilterRouteActions_CanFixBrokenRoute()
        {
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Hosts = new[] { "invalid host name" } }, Order = 1, ClusterId = "cluster1" };
            var services = CreateServices(new List<ProxyRoute>() { route1 }, new List<Cluster>(), proxyBuilder =>
            {
                proxyBuilder.AddProxyConfigFilter<FixRouteHostFilter>();
            });
            var configManager = services.GetRequiredService<ProxyConfigManager>();

            var dataSource = await configManager.InitialLoadAsync();
            var endpoints = dataSource.Endpoints;

            Assert.Single(endpoints);
            var endpoint = endpoints.Single();
            Assert.Same(route1.RouteId, endpoint.DisplayName);
            var hostMetadata = endpoint.Metadata.GetMetadata<HostAttribute>();
            Assert.NotNull(hostMetadata);
            var host = Assert.Single(hostMetadata.Hosts);
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
                cluster.HealthCheck = new HealthCheckOptions() { Active = new ActiveHealthCheckOptions { Enabled = true, Interval = TimeSpan.FromSeconds(12), Policy = "activePolicyA" } };
                return Task.CompletedTask;
            }

            public Task ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel)
            {
                route.Order = 12;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task LoadAsync_ConfigFilterConfiguresCluster_Works()
        {
            var cluster = new Cluster() { Id = "cluster1", Destinations = { { "d1", new Destination() { Address = "http://localhost" } } } };
            var services = CreateServices(new List<ProxyRoute>(), new List<Cluster>() { cluster }, proxyBuilder =>
            {
                proxyBuilder.AddProxyConfigFilter<ClusterAndRouteFilter>();
            });
            var configManager = services.GetRequiredService<ProxyConfigManager>();
            var clusterManager = services.GetRequiredService<IClusterManager>();

            var dataSource = await configManager.InitialLoadAsync();
            var endpoints = dataSource.Endpoints;

            var clusterInfo = clusterManager.TryGetItem("cluster1");

            Assert.NotNull(clusterInfo);
            Assert.True(clusterInfo.Config.HealthCheckOptions.Enabled);
            Assert.Equal(TimeSpan.FromSeconds(12), clusterInfo.Config.HealthCheckOptions.Active.Interval);
            var destination = Assert.Single(clusterInfo.DynamicState.AllDestinations);
            Assert.Equal("http://localhost", destination.Config.Address);
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
        public async Task LoadAsync_ConfigFilterClusterActionThrows_Throws()
        {
            var cluster = new Cluster() { Id = "cluster1", Destinations = { { "d1", new Destination() { Address = "http://localhost" } } } };
            var services = CreateServices(new List<ProxyRoute>(), new List<Cluster>() { cluster }, proxyBuilder =>
            {
                proxyBuilder.AddProxyConfigFilter<ClusterAndRouteThrows>();
                proxyBuilder.AddProxyConfigFilter<ClusterAndRouteThrows>();
            });
            var configManager = services.GetRequiredService<ProxyConfigManager>();

            var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
            Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);
            var agex = Assert.IsType<AggregateException>(ioEx.InnerException);

            Assert.Single(agex.InnerExceptions);
            Assert.IsType<NotFiniteNumberException>(agex.InnerExceptions.First().InnerException);
        }


        [Fact]
        public async Task LoadAsync_ConfigFilterRouteActionThrows_Throws()
        {
            var route1 = new ProxyRoute { RouteId = "route1", Match = { Hosts = new[] { "example.com" } }, Order = 1, ClusterId = "cluster1" };
            var route2 = new ProxyRoute { RouteId = "route2", Match = { Hosts = new[] { "example2.com" } }, Order = 1, ClusterId = "cluster2" };
            var services = CreateServices(new List<ProxyRoute>() { route1, route2 }, new List<Cluster>(), proxyBuilder =>
            {
                proxyBuilder.AddProxyConfigFilter<ClusterAndRouteThrows>();
                proxyBuilder.AddProxyConfigFilter<ClusterAndRouteThrows>();
            });
            var configManager = services.GetRequiredService<ProxyConfigManager>();

            var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
            Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);
            var agex = Assert.IsType<AggregateException>(ioEx.InnerException);

            Assert.Equal(2, agex.InnerExceptions.Count);
            Assert.IsType<NotFiniteNumberException>(agex.InnerExceptions.First().InnerException);
            Assert.IsType<NotFiniteNumberException>(agex.InnerExceptions.Skip(1).First().InnerException);
        }
    }
}

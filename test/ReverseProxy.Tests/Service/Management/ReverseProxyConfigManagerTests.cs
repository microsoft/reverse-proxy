// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Configuration;
using Microsoft.ReverseProxy.Utilities;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Management.Tests
{
    public class ReverseProxyConfigManagerTests
    {
        private IServiceProvider CreateServices(List<ProxyRoute> routes, List<Cluster> clusters)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddRouting();
            serviceCollection.AddReverseProxy().LoadFromMemory(routes, clusters);
            var services = serviceCollection.BuildServiceProvider();
            var routeBuilder = services.GetRequiredService<IRuntimeRouteBuilder>();
            routeBuilder.SetProxyPipeline(context => Task.CompletedTask);
            return services;
        }

        [Fact]
        public void Constructor_Works()
        {
            var services = CreateServices(new List<ProxyRoute>(), new List<Cluster>());
            _ = services.GetRequiredService<IProxyConfigManager>();
        }

        [Fact]
        public void Endpoints_StartsEmpty()
        {
            var services = CreateServices(new List<ProxyRoute>(), new List<Cluster>());
            var manager = services.GetRequiredService<IProxyConfigManager>();

            var dataSource = manager.DataSource;
            Assert.NotNull(dataSource);
            var endpoints = dataSource.Endpoints;
            Assert.Empty(endpoints);
        }

        [Fact]
        public void GetChangeToken_InitialValue()
        {
            var services = CreateServices(new List<ProxyRoute>(), new List<Cluster>());
            var manager = services.GetRequiredService<IProxyConfigManager>();

            var dataSource = manager.DataSource;
            Assert.NotNull(dataSource);
            var changeToken = dataSource.GetChangeToken();
            Assert.NotNull(changeToken);
            Assert.True(changeToken.ActiveChangeCallbacks);
            Assert.False(changeToken.HasChanged);
        }

        [Fact]
        public void BuildConfig_OneClusterOneDestinationOneRoute_Works()
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

            var manager = services.GetRequiredService<IProxyConfigManager>();
            manager.Load();

            var dataSource = manager.DataSource;
            Assert.NotNull(dataSource);
            var endpoints = dataSource.Endpoints;
            Assert.Single(endpoints);

            var clusterManager = services.GetRequiredService<IClusterManager>();
            var actualClusters = clusterManager.GetItems();
            Assert.Single(actualClusters);
            Assert.Equal("cluster1", actualClusters[0].ClusterId);
            Assert.NotNull(actualClusters[0].DestinationManager);
            Assert.NotNull(actualClusters[0].Config.Value);

            var actualDestinations = actualClusters[0].DestinationManager.GetItems();
            Assert.Single(actualDestinations);
            Assert.Equal("d1", actualDestinations[0].DestinationId);
            Assert.NotNull(actualDestinations[0].Config);
            Assert.Equal(TestAddress, actualDestinations[0].Config.Address);

            var routeManager = services.GetRequiredService<IRouteManager>();
            var actualRoutes = routeManager.GetItems();
            Assert.Single(actualRoutes);
            Assert.Equal("route1", actualRoutes[0].RouteId);
            Assert.NotNull(actualRoutes[0].Config.Value);
            Assert.Same(actualClusters[0], actualRoutes[0].Config.Value.Cluster);
        }

        [Fact]
        public async Task GetChangeToken_SignalsChange()
        {
            var services = CreateServices(new List<ProxyRoute>(), new List<Cluster>());
            var inMemoryConfig = (InMemoryConfigProvider)services.GetRequiredService<IProxyConfigProvider>();
            var configManager = services.GetRequiredService<IProxyConfigManager>();
            configManager.Load();
            var dataSource = configManager.DataSource;

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
    }
}

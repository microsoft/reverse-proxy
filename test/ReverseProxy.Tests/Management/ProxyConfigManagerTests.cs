// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Configuration.ConfigProvider;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Forwarder.Tests;
using Yarp.ReverseProxy.Routing;

namespace Yarp.ReverseProxy.Management.Tests;

public class ProxyConfigManagerTests
{
    private static IServiceProvider CreateServices(List<RouteConfig> routes, List<ClusterConfig> clusters, Action<IReverseProxyBuilder> configureProxy = null)
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

    private static IServiceProvider CreateServices(IEnumerable<IProxyConfigProvider> configProviders, Action<IReverseProxyBuilder> configureProxy = null)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddRouting();
        var proxyBuilder = serviceCollection.AddReverseProxy();
        foreach (var configProvider in configProviders)
        {
            serviceCollection.AddSingleton(configProvider);
        }
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
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>());
        _ = services.GetRequiredService<ProxyConfigManager>();
    }

    [Fact]
    public async Task NullRoutes_StartsEmpty()
    {
        var services = CreateServices(null, new List<ClusterConfig>());
        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();
        Assert.NotNull(dataSource);
        var endpoints = dataSource.Endpoints;
        Assert.Empty(endpoints);
    }

    [Fact]
    public async Task NullClusters_StartsEmpty()
    {
        var services = CreateServices(new List<RouteConfig>(), null);
        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();
        Assert.NotNull(dataSource);
        var endpoints = dataSource.Endpoints;
        Assert.Empty(endpoints);
    }

    [Fact]
    public async Task Endpoints_StartsEmpty()
    {
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>());
        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();
        Assert.NotNull(dataSource);
        var endpoints = dataSource.Endpoints;
        Assert.Empty(endpoints);
    }

    [Fact]
    public async Task Lookup_StartsEmpty()
    {
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>());
        var manager = services.GetRequiredService<ProxyConfigManager>();
        var lookup = services.GetRequiredService<IProxyStateLookup>();
        await manager.InitialLoadAsync();

        Assert.Empty(lookup.GetRoutes());
        Assert.Empty(lookup.GetClusters());
        Assert.False(lookup.TryGetRoute("route1", out var _));
        Assert.False(lookup.TryGetCluster("cluster1", out var _));
    }

    [Fact]
    public async Task GetChangeToken_InitialValue()
    {
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>());
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

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig { Address = TestAddress } }
            }
        };
        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };

        var services = CreateServices(new List<RouteConfig>() { route }, new List<ClusterConfig>() { cluster });

        var manager = services.GetRequiredService<ProxyConfigManager>();
        var lookup = services.GetRequiredService<IProxyStateLookup>();
        var dataSource = await manager.InitialLoadAsync();

        Assert.NotNull(dataSource);
        var endpoints = dataSource.Endpoints;
        var endpoint = Assert.Single(endpoints);
        var routeConfig = endpoint.Metadata.GetMetadata<RouteModel>();
        Assert.NotNull(routeConfig);
        Assert.Equal("route1", routeConfig.Config.RouteId);
        Assert.True(lookup.TryGetRoute("route1", out var routeModel));
        Assert.Equal(route, routeModel.Config);
        routeModel = Assert.Single(lookup.GetRoutes());
        Assert.Equal(route, routeModel.Config);

        var clusterState = routeConfig.Cluster;
        Assert.NotNull(clusterState);

        Assert.Equal("cluster1", clusterState.ClusterId);
        Assert.NotNull(clusterState.Destinations);
        Assert.NotNull(clusterState.Model);
        Assert.NotNull(clusterState.Model.HttpClient);
        Assert.Same(clusterState, routeConfig.Cluster);
        Assert.True(lookup.TryGetCluster("cluster1", out clusterState));
        Assert.Equal(cluster, clusterState.Model.Config);
        clusterState = Assert.Single(lookup.GetClusters());
        Assert.Equal(cluster, clusterState.Model.Config);

        var actualDestinations = clusterState.Destinations.Values;
        var destination = Assert.Single(actualDestinations);
        Assert.Equal("d1", destination.DestinationId);
        Assert.NotNull(destination.Model);
        Assert.Equal(TestAddress, destination.Model.Config.Address);
    }

    [Fact]
    public async Task BuildConfig_TwoDistinctConfigs_Works()
    {
        const string TestAddress = "https://localhost:123/";

        var cluster1 = new ClusterConfig
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig { Address = TestAddress } }
            }
        };
        var route1 = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };

        var cluster2 = new ClusterConfig
        {
            ClusterId = "cluster2",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d2", new DestinationConfig { Address = TestAddress } }
            }
        };
        var route2 = new RouteConfig
        {
            RouteId = "route2",
            ClusterId = "cluster2",
            Match = new RouteMatch { Path = "/" }
        };

        var config1 = new InMemoryConfigProvider(new List<RouteConfig>() { route1 }, new List<ClusterConfig>() { cluster1 });
        var config2 = new InMemoryConfigProvider(new List<RouteConfig>() { route2 }, new List<ClusterConfig>() { cluster2 });

        var services = CreateServices(new[] { config1, config2 });

        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();

        Assert.NotNull(dataSource);
        var endpoints = dataSource.Endpoints;
        Assert.Equal(2, endpoints.Count);

        // The order is unstable because routes are stored in a dictionary.
        var routeConfig = endpoints.Single(e => string.Equals(e.DisplayName, "route1")).Metadata.GetMetadata<RouteModel>();
        Assert.NotNull(routeConfig);
        Assert.Equal("route1", routeConfig.Config.RouteId);

        var clusterState = routeConfig.Cluster;
        Assert.NotNull(clusterState);

        Assert.Equal("cluster1", clusterState.ClusterId);
        Assert.NotNull(clusterState.Destinations);
        Assert.NotNull(clusterState.Model);
        Assert.NotNull(clusterState.Model.HttpClient);
        Assert.Same(clusterState, routeConfig.Cluster);

        var actualDestinations = clusterState.Destinations.Values;
        var destination = Assert.Single(actualDestinations);
        Assert.Equal("d1", destination.DestinationId);
        Assert.NotNull(destination.Model);
        Assert.Equal(TestAddress, destination.Model.Config.Address);

        routeConfig = endpoints.Single(e => string.Equals(e.DisplayName, "route2")).Metadata.GetMetadata<RouteModel>();
        Assert.NotNull(routeConfig);
        Assert.Equal("route2", routeConfig.Config.RouteId);

        clusterState = routeConfig.Cluster;
        Assert.NotNull(clusterState);

        Assert.Equal("cluster2", clusterState.ClusterId);
        Assert.NotNull(clusterState.Destinations);
        Assert.NotNull(clusterState.Model);
        Assert.NotNull(clusterState.Model.HttpClient);
        Assert.Same(clusterState, routeConfig.Cluster);

        actualDestinations = clusterState.Destinations.Values;
        destination = Assert.Single(actualDestinations);
        Assert.Equal("d2", destination.DestinationId);
        Assert.NotNull(destination.Model);
        Assert.Equal(TestAddress, destination.Model.Config.Address);
    }

    [Fact]
    public async Task BuildConfig_TwoOverlappingConfigs_Works()
    {
        const string TestAddress = "https://localhost:123/";

        var cluster1 = new ClusterConfig
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig { Address = TestAddress } }
            }
        };
        var cluster2 = new ClusterConfig
        {
            ClusterId = "cluster2",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d2", new DestinationConfig { Address = TestAddress } }
            }
        };

        var route1 = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };
        var route2 = new RouteConfig
        {
            RouteId = "route2",
            ClusterId = "cluster2",
            Match = new RouteMatch { Path = "/" }
        };

        var config1 = new InMemoryConfigProvider(new List<RouteConfig>() { route2 }, new List<ClusterConfig>() { cluster1 });
        var config2 = new InMemoryConfigProvider(new List<RouteConfig>() { route1 }, new List<ClusterConfig>() { cluster2 });

        var services = CreateServices(new[] { config1, config2 });

        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();

        Assert.NotNull(dataSource);
        var endpoints = dataSource.Endpoints;
        Assert.Equal(2, endpoints.Count);

        // The order is unstable because routes are stored in a dictionary.
        var routeConfig = endpoints.Single(e => string.Equals(e.DisplayName, "route1")).Metadata.GetMetadata<RouteModel>();
        Assert.NotNull(routeConfig);
        Assert.Equal("route1", routeConfig.Config.RouteId);

        var clusterState = routeConfig.Cluster;
        Assert.NotNull(clusterState);

        Assert.Equal("cluster1", clusterState.ClusterId);
        Assert.NotNull(clusterState.Destinations);
        Assert.NotNull(clusterState.Model);
        Assert.NotNull(clusterState.Model.HttpClient);
        Assert.Same(clusterState, routeConfig.Cluster);

        var actualDestinations = clusterState.Destinations.Values;
        var destination = Assert.Single(actualDestinations);
        Assert.Equal("d1", destination.DestinationId);
        Assert.NotNull(destination.Model);
        Assert.Equal(TestAddress, destination.Model.Config.Address);

        routeConfig = endpoints.Single(e => string.Equals(e.DisplayName, "route2")).Metadata.GetMetadata<RouteModel>();
        Assert.NotNull(routeConfig);
        Assert.Equal("route2", routeConfig.Config.RouteId);

        clusterState = routeConfig.Cluster;
        Assert.NotNull(clusterState);

        Assert.Equal("cluster2", clusterState.ClusterId);
        Assert.NotNull(clusterState.Destinations);
        Assert.NotNull(clusterState.Model);
        Assert.NotNull(clusterState.Model.HttpClient);
        Assert.Same(clusterState, routeConfig.Cluster);

        actualDestinations = clusterState.Destinations.Values;
        destination = Assert.Single(actualDestinations);
        Assert.Equal("d2", destination.DestinationId);
        Assert.NotNull(destination.Model);
        Assert.Equal(TestAddress, destination.Model.Config.Address);
    }

    [Fact]
    public async Task InitialLoadAsync_ProxyHttpClientOptionsSet_CreateAndSetHttpClient()
    {
        const string TestAddress = "https://localhost:123/";

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig { Address = TestAddress } }
            },
            HttpClient = new HttpClientConfig
            {
                SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                MaxConnectionsPerServer = 10,
#if NET
                RequestHeaderEncoding = Encoding.UTF8.WebName
#endif
            },
            HealthCheck = new HealthCheckConfig
            {
                Active = new ActiveHealthCheckConfig { Enabled = true }
            }
        };
        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };

        var services = CreateServices(new List<RouteConfig>() { route }, new List<ClusterConfig>() { cluster });

        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();

        Assert.NotNull(dataSource);
        var endpoint = Assert.Single(dataSource.Endpoints);
        var routeConfig = endpoint.Metadata.GetMetadata<RouteModel>();
        var clusterState = routeConfig.Cluster;
        Assert.Equal("cluster1", clusterState.ClusterId);
        var clusterModel = clusterState.Model;
        Assert.NotNull(clusterModel.HttpClient);
        Assert.Equal(SslProtocols.Tls11 | SslProtocols.Tls12, clusterModel.Config.HttpClient.SslProtocols);
        Assert.Equal(10, clusterModel.Config.HttpClient.MaxConnectionsPerServer);
#if NET
        Assert.Equal(Encoding.UTF8.WebName, clusterModel.Config.HttpClient.RequestHeaderEncoding);
#endif

        var handler = ForwarderHttpClientFactoryTests.GetHandler(clusterModel.HttpClient);
        Assert.Equal(SslProtocols.Tls11 | SslProtocols.Tls12, handler.SslOptions.EnabledSslProtocols);
        Assert.Equal(10, handler.MaxConnectionsPerServer);
#if NET
        Assert.Equal(Encoding.UTF8, handler.RequestHeaderEncodingSelector(default, default));
#endif
        var activeMonitor = (ActiveHealthCheckMonitor)services.GetRequiredService<IActiveHealthCheckMonitor>();
        Assert.True(activeMonitor.Scheduler.IsScheduled(clusterState));
    }

    [Fact]
    public async Task GetChangeToken_SignalsChange()
    {
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>());
        var inMemoryConfig = (InMemoryConfigProvider)services.GetRequiredService<IProxyConfigProvider>();
        var configManager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await configManager.InitialLoadAsync();
        _ = configManager.Endpoints; // Lazily creates endpoints the first time, activates change notifications.

        var signaled1 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var signaled2 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        IReadOnlyList<Endpoint> readEndpoints1 = null;
        IReadOnlyList<Endpoint> readEndpoints2 = null;

        var changeToken1 = dataSource.GetChangeToken();
        changeToken1.RegisterChangeCallback(
            _ =>
            {
                readEndpoints1 = dataSource.Endpoints;
                signaled1.SetResult(1);
            }, null);

        // updating should signal the current change token
        Assert.False(signaled1.Task.IsCompleted);
        inMemoryConfig.Update(new List<RouteConfig>() { new RouteConfig() { RouteId = "r1", Match = new RouteMatch { Path = "/" } } }, new List<ClusterConfig>());
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
        inMemoryConfig.Update(new List<RouteConfig>() { new RouteConfig() { RouteId = "r2", Match = new RouteMatch { Path = "/" } } }, new List<ClusterConfig>());
        await signaled2.Task.DefaultTimeout();

        Assert.NotNull(readEndpoints1);
        Assert.NotNull(readEndpoints2);
    }

    [Fact]
    public async Task GetChangeToken_MultipleConfigs_SignalsChange()
    {
        var config1 = new InMemoryConfigProvider(new List<RouteConfig>(), new List<ClusterConfig>());
        var config2 = new InMemoryConfigProvider(new List<RouteConfig>(), new List<ClusterConfig>());
        var services = CreateServices(new[] { config1, config2 });
        var configManager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await configManager.InitialLoadAsync();
        _ = configManager.Endpoints; // Lazily creates endpoints the first time, activates change notifications.

        var signaled1 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var signaled2 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        IReadOnlyList<Endpoint> readEndpoints1 = null;
        IReadOnlyList<Endpoint> readEndpoints2 = null;

        var changeToken1 = dataSource.GetChangeToken();
        changeToken1.RegisterChangeCallback(
            _ =>
            {
                readEndpoints1 = dataSource.Endpoints;
                signaled1.SetResult(1);
            }, null);

        // updating should signal the current change token
        Assert.False(signaled1.Task.IsCompleted);
        config1.Update(new List<RouteConfig>() { new RouteConfig() { RouteId = "r1", Match = new RouteMatch { Path = "/" } } }, new List<ClusterConfig>());
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
        config2.Update(new List<RouteConfig>() { new RouteConfig() { RouteId = "r2", Match = new RouteMatch { Path = "/" } } }, new List<ClusterConfig>());
        await signaled2.Task.DefaultTimeout();

        var endpoint = Assert.Single(readEndpoints1);
        Assert.Equal("r1", endpoint.DisplayName);

        Assert.NotNull(readEndpoints2);
        Assert.Equal(2, readEndpoints2.Count);
        // Ordering is unstable due to dictionary storage.
        readEndpoints2.Single(e => string.Equals(e.DisplayName, "r1"));
        readEndpoints2.Single(e => string.Equals(e.DisplayName, "r2"));
    }

    [Fact]
    public async Task ChangeConfig_ActiveHealthCheckIsEnabled_RunInitialCheck()
    {
        var endpoints = new List<RouteConfig>() { new RouteConfig() { RouteId = "r1", ClusterId = "c1", Match = new RouteMatch { Path = "/" } } };
        var clusters = new List<ClusterConfig>() { new ClusterConfig { ClusterId = "c1" } };
        var services = CreateServices(endpoints, clusters);
        var inMemoryConfig = (InMemoryConfigProvider)services.GetRequiredService<IProxyConfigProvider>();
        var configManager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await configManager.InitialLoadAsync();

        var endpoint = Assert.Single(dataSource.Endpoints);
        var routeConfig = endpoint.Metadata.GetMetadata<RouteModel>();
        var clusterState = routeConfig.Cluster;
        var activeMonitor = (ActiveHealthCheckMonitor)services.GetRequiredService<IActiveHealthCheckMonitor>();
        Assert.False(activeMonitor.Scheduler.IsScheduled(clusterState));

        var signaled = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changeToken = dataSource.GetChangeToken();
        changeToken.RegisterChangeCallback(
            _ =>
            {
                signaled.SetResult(1);
            }, null);

        // updating should signal the current change token
        Assert.False(signaled.Task.IsCompleted);
        inMemoryConfig.Update(
            endpoints,
            new List<ClusterConfig>()
            {
                new ClusterConfig
                {
                    ClusterId = "c1",
                    HealthCheck = new HealthCheckConfig { Active = new ActiveHealthCheckConfig { Enabled = true } }
                }
            });
        await signaled.Task.DefaultTimeout();

        Assert.True(activeMonitor.Scheduler.IsScheduled(clusterState));
    }

    [Fact]
    public async Task LoadAsync_RequestVersionValidationError_Throws()
    {
        const string TestAddress = "https://localhost:123/";

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig { Address = TestAddress } }
            },
            HttpRequest = new ForwarderRequestConfig() { Version = new Version(1, 2) }
        };

        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>() { cluster });
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
        var routeName = "route1";
        var route1 = new RouteConfig { RouteId = routeName, Match = new RouteMatch { Hosts = null }, ClusterId = "cluster1" };
        var services = CreateServices(new List<RouteConfig>() { route1 }, new List<ClusterConfig>());
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
        Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);
        var agex = Assert.IsType<AggregateException>(ioEx.InnerException);

        Assert.Single(agex.InnerExceptions);
        var argex = Assert.IsType<ArgumentException>(agex.InnerExceptions.First());
        Assert.StartsWith($"Route '{routeName}' requires Hosts or Path specified", argex.Message);
    }

    [Fact]
    public async Task LoadAsync_MultipleSourcesWithValidationErrors_Throws()
    {
        var route1 = new RouteConfig { RouteId = "route1", Match = new RouteMatch { Hosts = null }, ClusterId = "cluster1" };
        var provider1 = new InMemoryConfigProvider(new List<RouteConfig>() { route1 }, new List<ClusterConfig>());
        var cluster1 = new ClusterConfig { ClusterId = "cluster1", HttpClient = new HttpClientConfig { MaxConnectionsPerServer = -1 } };
        var provider2 = new InMemoryConfigProvider(new List<RouteConfig>(), new List<ClusterConfig>() { cluster1 });
        var services = CreateServices(new[] { provider1, provider2 });
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
        Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);
        var agex = Assert.IsType<AggregateException>(ioEx.InnerException);

        Assert.Equal(2, agex.InnerExceptions.Count);
        var argex = Assert.IsType<ArgumentException>(agex.InnerExceptions.First());
        Assert.StartsWith($"Route 'route1' requires Hosts or Path specified", argex.Message);
        argex = Assert.IsType<ArgumentException>(agex.InnerExceptions.Skip(1).First());
        Assert.StartsWith($"Max connections per server limit set on the cluster 'cluster1' must be positive.", argex.Message);
    }

    [Fact]
    public async Task LoadAsync_ConfigFilterRouteActions_CanFixBrokenRoute()
    {
        var route1 = new RouteConfig { RouteId = "route1", Match = new RouteMatch { Hosts = null }, Order = 1, ClusterId = "cluster1" };
        var services = CreateServices(new List<RouteConfig>() { route1 }, new List<ClusterConfig>(), proxyBuilder =>
        {
            proxyBuilder.AddConfigFilter<FixRouteHostFilter>();
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
        public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
        {
            return new ValueTask<ClusterConfig>(cluster);
        }

        public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig cluster, CancellationToken cancel)
        {
            return new ValueTask<RouteConfig>(route with
            {
                Match = route.Match with { Hosts = new[] { "example.com" } }
            });
        }
    }

    private class ClusterAndRouteFilter : IProxyConfigFilter
    {
        public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
        {
            return new ValueTask<ClusterConfig>(cluster with
            {
                HealthCheck = new HealthCheckConfig()
                {
                    Active = new ActiveHealthCheckConfig { Enabled = true, Interval = TimeSpan.FromSeconds(12), Policy = "activePolicyA" }
                }
            });
        }

        public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig cluster, CancellationToken cancel)
        {
            string order;
            if (cluster != null)
            {
                order = cluster.Metadata["Order"];
            }
            else
            {
                order = "12";
            }

            return new ValueTask<RouteConfig>(route with { Order = int.Parse(order) });
        }
    }

    [Fact]
    public async Task LoadAsync_ConfigFilterConfiguresCluster_Works()
    {
        var route1 = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };
        var route2 = new RouteConfig
        {
            RouteId = "route2",
            ClusterId = "cluster2",
            Match = new RouteMatch { Path = "/" }
        };
        var cluster = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig() { Address = "http://localhost" } }
            },
            Metadata = new Dictionary<string, string>
            {
                ["Order"] = "47"
            }
        };
        var services = CreateServices(new List<RouteConfig>() { route1, route2 }, new List<ClusterConfig>() { cluster }, proxyBuilder =>
        {
            proxyBuilder.AddConfigFilter<ClusterAndRouteFilter>();
        });
        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();

        Assert.NotNull(dataSource);
        Assert.Equal(2, dataSource.Endpoints.Count);

        var endpoint1 = Assert.Single(dataSource.Endpoints.Where(x => x.DisplayName == "route1"));
        var routeConfig1 = endpoint1.Metadata.GetMetadata<RouteModel>();
        Assert.Equal(47, routeConfig1.Config.Order);
        var clusterState1 = routeConfig1.Cluster;
        Assert.NotNull(clusterState1);
        Assert.True(clusterState1.Model.Config.HealthCheck.Active.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(12), clusterState1.Model.Config.HealthCheck.Active.Interval);
        var destination = Assert.Single(clusterState1.DestinationsState.AllDestinations);
        Assert.Equal("http://localhost", destination.Model.Config.Address);

        var endpoint2 = Assert.Single(dataSource.Endpoints.Where(x => x.DisplayName == "route2"));
        var routeConfig2 = endpoint2.Metadata.GetMetadata<RouteModel>();
        Assert.Equal(12, routeConfig2.Config.Order);
    }

    private class ClusterAndRouteThrows : IProxyConfigFilter
    {
        public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
        {
            throw new NotFiniteNumberException("Test exception");
        }

        public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig cluster, CancellationToken cancel)
        {
            throw new NotFiniteNumberException("Test exception");
        }
    }

    [Fact]
    public async Task LoadAsync_ConfigFilterClusterActionThrows_Throws()
    {
        var cluster = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig() { Address = "http://localhost" } }
            }
        };
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>() { cluster }, proxyBuilder =>
        {
            proxyBuilder.AddConfigFilter<ClusterAndRouteThrows>();
            proxyBuilder.AddConfigFilter<ClusterAndRouteThrows>();
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
        var route1 = new RouteConfig { RouteId = "route1", Match = new RouteMatch { Hosts = new[] { "example.com" } }, Order = 1, ClusterId = "cluster1" };
        var route2 = new RouteConfig { RouteId = "route2", Match = new RouteMatch { Hosts = new[] { "example2.com" } }, Order = 1, ClusterId = "cluster2" };
        var services = CreateServices(new List<RouteConfig>() { route1, route2 }, new List<ClusterConfig>(), proxyBuilder =>
        {
            proxyBuilder.AddConfigFilter<ClusterAndRouteThrows>();
            proxyBuilder.AddConfigFilter<ClusterAndRouteThrows>();
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

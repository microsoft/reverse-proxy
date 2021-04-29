using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service.Management;
using Yarp.ReverseProxy.Service.Proxy;
using Yarp.ReverseProxy.Service.Routing;

namespace Yarp.ReverseProxy.Service.DynamicEndpoint
{
    public class ProxyEndpointFactoryTests
    {
        private IServiceProvider CreateServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ProxyEndpointFactory, ProxyEndpointFactory>();
            return serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Constructor_Works()
        {
            var services = CreateServices();
            _ = services.GetRequiredService<ProxyEndpointFactory>();
        }

        [Fact]
        public void AddEndpoint_HostAndPath_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                Match = new RouteMatch
                {
                    Hosts = new[] { "example.com" },
                    Path = "/a",
                },
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

            Assert.Same(cluster, routeConfig.Cluster);
            Assert.Equal("route1", routeEndpoint.DisplayName);
            Assert.Same(routeConfig, routeEndpoint.Metadata.GetMetadata<RouteModel>());
            Assert.Equal("/a", routeEndpoint.RoutePattern.RawText);
            Assert.Equal(12, routeEndpoint.Order);
            Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<HostAttribute>();
            Assert.NotNull(hostMetadata);
            Assert.Single(hostMetadata.Hosts);
            Assert.Equal("example.com", hostMetadata.Hosts[0]);
        }

        private (RouteEndpoint routeEndpoint, RouteModel routeConfig) CreateEndpoint(ProxyEndpointFactory factory, RouteState routeState, RouteConfig proxyRoute, ClusterInfo clusterInfo)
        {
            routeState.ClusterRevision = clusterInfo.Revision;
            var routeConfig = new RouteModel(proxyRoute, clusterInfo, HttpTransformer.Default);

            var endpoint = factory.CreateEndpoint(routeConfig, Array.Empty<Action<EndpointBuilder>>());

            var routeEndpoint = Assert.IsType<RouteEndpoint>(endpoint);

            return (routeEndpoint, routeConfig);
        }

        [Fact]
        public void AddEndpoint_JustHost_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                Match = new RouteMatch
                {
                    Hosts = new[] { "example.com" },
                },
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

            Assert.Same(cluster, routeConfig.Cluster);
            Assert.Equal("route1", routeEndpoint.DisplayName);
            Assert.Same(routeConfig, routeEndpoint.Metadata.GetMetadata<RouteModel>());
            Assert.Equal("/{**catchall}", routeEndpoint.RoutePattern.RawText);
            Assert.Equal(12, routeEndpoint.Order);
            Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<HostAttribute>();
            Assert.NotNull(hostMetadata);
            Assert.Single(hostMetadata.Hosts);
            Assert.Equal("example.com", hostMetadata.Hosts[0]);
        }

        [Fact]
        public void AddEndpoint_JustHostWithWildcard_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                Match = new RouteMatch
                {
                    Hosts = new[] { "*.example.com" },
                },
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

            Assert.Same(cluster, routeConfig.Cluster);
            Assert.Equal("route1", routeEndpoint.DisplayName);
            Assert.Same(routeConfig, routeEndpoint.Metadata.GetMetadata<RouteModel>());
            Assert.Equal("/{**catchall}", routeEndpoint.RoutePattern.RawText);
            Assert.Equal(12, routeEndpoint.Order);
            Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<HostAttribute>();
            Assert.NotNull(hostMetadata);
            Assert.Single(hostMetadata.Hosts);
            Assert.Equal("*.example.com", hostMetadata.Hosts[0]);
        }

        [Fact]
        public void AddEndpoint_JustPath_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                Match = new RouteMatch
                {
                    Path = "/a",
                },
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

            Assert.Same(cluster, routeConfig.Cluster);
            Assert.Equal("route1", routeEndpoint.DisplayName);
            Assert.Same(routeConfig, routeEndpoint.Metadata.GetMetadata<RouteModel>());
            Assert.Equal("/a", routeEndpoint.RoutePattern.RawText);
            Assert.Equal(12, routeEndpoint.Order);
            Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<HostAttribute>();
            Assert.Null(hostMetadata);
        }

        [Fact]
        public void AddEndpoint_NullMatchers_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                Order = 12,
                Match = new RouteMatch()
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

            Assert.Same(cluster, routeConfig.Cluster);
            Assert.Equal("route1", routeEndpoint.DisplayName);
            Assert.Same(routeConfig, routeEndpoint.Metadata.GetMetadata<RouteModel>());
            Assert.Equal("/{**catchall}", routeEndpoint.RoutePattern.RawText);
            Assert.Equal(12, routeEndpoint.Order);
            Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<HostAttribute>();
            Assert.Null(hostMetadata);
        }

        [Fact]
        public void AddEndpoint_InvalidPath_BubblesOutException()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                Match = new RouteMatch
                {
                    Path = "/{invalid",
                },
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            Action action = () => CreateEndpoint(factory, routeState, route, cluster);

            Assert.Throws<RoutePatternException>(action);
        }

        [Fact]
        public void AddEndpoint_DefaultAuth_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                AuthorizationPolicy = "defaulT",
                Order = 12,
                Match = new RouteMatch(),
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

            var attribute = Assert.IsType<AuthorizeAttribute>(routeEndpoint.Metadata.GetMetadata<IAuthorizeData>());
            Assert.Null(attribute.Policy);
        }

        [Fact]
        public void AddEndpoint_AnonymousAuth_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                AuthorizationPolicy = "AnonymouS",
                Order = 12,
                Match = new RouteMatch(),
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

            Assert.IsType<AllowAnonymousAttribute>(routeEndpoint.Metadata.GetMetadata<IAllowAnonymous>());
        }

        [Fact]
        public void AddEndpoint_CustomAuth_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                AuthorizationPolicy = "custom",
                Order = 12,
                Match = new RouteMatch(),
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

            var attribute = Assert.IsType<AuthorizeAttribute>(routeEndpoint.Metadata.GetMetadata<IAuthorizeData>());
            Assert.Equal("custom", attribute.Policy);
        }

        [Fact]
        public void AddEndpoint_NoAuth_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                Order = 12,
                Match = new RouteMatch(),
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

            Assert.Null(routeEndpoint.Metadata.GetMetadata<IAuthorizeData>());
            Assert.Null(routeEndpoint.Metadata.GetMetadata<IAllowAnonymous>());
        }

        [Fact]
        public void AddEndpoint_DefaultCors_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                CorsPolicy = "defaulT",
                Order = 12,
                Match = new RouteMatch(),
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

            var attribute = Assert.IsType<EnableCorsAttribute>(routeEndpoint.Metadata.GetMetadata<IEnableCorsAttribute>());
            Assert.Null(attribute.PolicyName);
            Assert.Null(routeEndpoint.Metadata.GetMetadata<IDisableCorsAttribute>());
        }

        [Fact]
        public void AddEndpoint_CustomCors_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                CorsPolicy = "custom",
                Order = 12,
                Match = new RouteMatch(),
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

            var attribute = Assert.IsType<EnableCorsAttribute>(routeEndpoint.Metadata.GetMetadata<IEnableCorsAttribute>());
            Assert.Equal("custom", attribute.PolicyName);
            Assert.Null(routeEndpoint.Metadata.GetMetadata<IDisableCorsAttribute>());
        }

        [Fact]
        public void AddEndpoint_DisableCors_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                CorsPolicy = "disAble",
                Order = 12,
                Match = new RouteMatch(),
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

            Assert.IsType<DisableCorsAttribute>(routeEndpoint.Metadata.GetMetadata<IDisableCorsAttribute>());
            Assert.Null(routeEndpoint.Metadata.GetMetadata<IEnableCorsAttribute>());
        }

        [Fact]
        public void AddEndpoint_NoCors_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                Order = 12,
                Match = new RouteMatch(),
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

            Assert.Null(routeEndpoint.Metadata.GetMetadata<IEnableCorsAttribute>());
            Assert.Null(routeEndpoint.Metadata.GetMetadata<IDisableCorsAttribute>());
        }

        [Fact]
        public void BuildEndpoints_Header_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                Match = new RouteMatch
                {
                    Path = "/",
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "header1",
                            Values = new[] { "value1" },
                            Mode = HeaderMatchMode.HeaderPrefix,
                            IsCaseSensitive = true,
                        }
                    }
                },
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

            Assert.Same(cluster, routeConfig.Cluster);
            Assert.Equal("route1", routeEndpoint.DisplayName);
            var headerMetadata = routeEndpoint.Metadata.GetMetadata<IHeaderMetadata>();
            Assert.NotNull(headerMetadata);
            var matchers = headerMetadata.Matchers;
            Assert.Single(matchers);
            var matcher = matchers.Single();
            Assert.Equal("header1", matcher.Name);
            Assert.Equal(new[] { "value1" }, matcher.Values);
            Assert.Equal(HeaderMatchMode.HeaderPrefix, matcher.Mode);
            Assert.True(matcher.IsCaseSensitive);

            Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));
        }

        [Fact]
        public void BuildEndpoints_Headers_Works()
        {
            var services = CreateServices();
            var factory = services.GetRequiredService<ProxyEndpointFactory>();
            factory.SetProxyPipeline(context => Task.CompletedTask);

            var route = new RouteConfig
            {
                RouteId = "route1",
                Match = new RouteMatch
                {
                    Path = "/",
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "header1",
                            Values = new[] { "value1" },
                            Mode = HeaderMatchMode.HeaderPrefix,
                            IsCaseSensitive = true,
                        },
                        new RouteHeader()
                        {
                            Name = "header2",
                            Mode = HeaderMatchMode.Exists,
                        }
                    }
                },
            };
            var cluster = new ClusterInfo("cluster1");
            var routeState = new RouteState("route1");

            var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

            Assert.Same(cluster, routeConfig.Cluster);
            Assert.Equal("route1", routeEndpoint.DisplayName);
            var metadata = routeEndpoint.Metadata.GetMetadata<IHeaderMetadata>();
            Assert.Equal(2, metadata.Matchers.Count);

            var firstMetadata = metadata.Matchers.First();
            Assert.NotNull(firstMetadata);
            Assert.Equal("header1", firstMetadata.Name);
            Assert.Equal(new[] { "value1" }, firstMetadata.Values);
            Assert.Equal(HeaderMatchMode.HeaderPrefix, firstMetadata.Mode);
            Assert.True(firstMetadata.IsCaseSensitive);

            var secondMetadata = metadata.Matchers.Skip(1).Single();
            Assert.NotNull(secondMetadata);
            Assert.Equal("header2", secondMetadata.Name);
            Assert.Null(secondMetadata.Values);
            Assert.Equal(HeaderMatchMode.Exists, secondMetadata.Mode);
            Assert.False(secondMetadata.IsCaseSensitive);

            Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));
        }
    }
}

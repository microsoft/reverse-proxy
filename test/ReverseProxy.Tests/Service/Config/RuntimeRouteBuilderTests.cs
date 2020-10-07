// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Routing;
using Microsoft.ReverseProxy.Utilities;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Tests
{
    public class RuntimeRouteBuilderTests
    {
        private IServiceProvider CreateServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IRuntimeRouteBuilder, RuntimeRouteBuilder>();
            serviceCollection.AddSingleton<ITransformBuilder, TransformBuilder>();
            serviceCollection.AddSingleton<IRandomFactory, RandomFactory>();
            serviceCollection.AddRouting();
            serviceCollection.AddLogging();
            return serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Constructor_Works()
        {
            var services = CreateServices();
            _ = services.GetRequiredService<IRuntimeRouteBuilder>();
        }

        [Fact]
        public void BuildEndpoints_HostAndPath_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Hosts = new[] { "example.com" },
                    Path = "/a",
                },
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Same(cluster, config.Cluster);
            Assert.Equal(12, config.Order);
            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
            Assert.Equal("route1", routeEndpoint.DisplayName);
            Assert.Same(config, routeEndpoint.Metadata.GetMetadata<RouteConfig>());
            Assert.Equal("/a", routeEndpoint.RoutePattern.RawText);
            Assert.Equal(12, routeEndpoint.Order);
            Assert.False(config.HasConfigChanged(route, cluster));

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<AspNetCore.Routing.HostAttribute>();
            Assert.NotNull(hostMetadata);
            Assert.Single(hostMetadata.Hosts);
            Assert.Equal("example.com", hostMetadata.Hosts[0]);
        }

        [Fact]
        public void BuildEndpoints_JustHost_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Hosts = new[] { "example.com" },
                },
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Same(cluster, config.Cluster);
            Assert.Equal(12, config.Order);
            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
            Assert.Equal("route1", routeEndpoint.DisplayName);
            Assert.Same(config, routeEndpoint.Metadata.GetMetadata<RouteConfig>());
            Assert.Equal("/{**catchall}", routeEndpoint.RoutePattern.RawText);
            Assert.Equal(12, routeEndpoint.Order);
            Assert.False(config.HasConfigChanged(route, cluster));

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<AspNetCore.Routing.HostAttribute>();
            Assert.NotNull(hostMetadata);
            Assert.Single(hostMetadata.Hosts);
            Assert.Equal("example.com", hostMetadata.Hosts[0]);
        }

        [Fact]
        public void BuildEndpoints_JustHostWithWildcard_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Hosts = new[] { "*.example.com" },
                },
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Same(cluster, config.Cluster);
            Assert.Equal(12, config.Order);
            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
            Assert.Equal("route1", routeEndpoint.DisplayName);
            Assert.Same(config, routeEndpoint.Metadata.GetMetadata<RouteConfig>());
            Assert.Equal("/{**catchall}", routeEndpoint.RoutePattern.RawText);
            Assert.Equal(12, routeEndpoint.Order);
            Assert.False(config.HasConfigChanged(route, cluster));

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<AspNetCore.Routing.HostAttribute>();
            Assert.NotNull(hostMetadata);
            Assert.Single(hostMetadata.Hosts);
            Assert.Equal("*.example.com", hostMetadata.Hosts[0]);
        }

        [Fact]
        public void BuildEndpoints_JustPath_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Path = "/a",
                },
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Same(cluster, config.Cluster);
            Assert.Equal(12, config.Order);
            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
            Assert.Equal("route1", routeEndpoint.DisplayName);
            Assert.Same(config, routeEndpoint.Metadata.GetMetadata<RouteConfig>());
            Assert.Equal("/a", routeEndpoint.RoutePattern.RawText);
            Assert.Equal(12, routeEndpoint.Order);
            Assert.False(config.HasConfigChanged(route, cluster));

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<AspNetCore.Routing.HostAttribute>();
            Assert.Null(hostMetadata);
        }

        [Fact]
        public void BuildEndpoints_NullMatchers_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Same(cluster, config.Cluster);
            Assert.Equal(12, config.Order);
            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
            Assert.Equal("route1", routeEndpoint.DisplayName);
            Assert.Same(config, routeEndpoint.Metadata.GetMetadata<RouteConfig>());
            Assert.Equal("/{**catchall}", routeEndpoint.RoutePattern.RawText);
            Assert.Equal(12, routeEndpoint.Order);
            Assert.False(config.HasConfigChanged(route, cluster));

            var hostMetadata = routeEndpoint.Metadata.GetMetadata<AspNetCore.Routing.HostAttribute>();
            Assert.Null(hostMetadata);
        }

        [Fact]
        public void BuildEndpoints_InvalidPath_BubblesOutException()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
                {
                    Path = "/{invalid",
                },
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            Action action = () => builder.Build(route, cluster, routeInfo);

            Assert.Throws<AspNetCore.Routing.Patterns.RoutePatternException>(action);
        }

        [Fact]
        public void BuildEndpoints_Header_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
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
            var cluster = new ClusterInfo("cluster1", new DestinationManager());
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Same(cluster, config.Cluster);
            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
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

            Assert.False(config.HasConfigChanged(route, cluster));
        }

        [Fact]
        public void BuildEndpoints_Headers_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                Match =
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
            var cluster = new ClusterInfo("cluster1", new DestinationManager());
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Same(cluster, config.Cluster);
            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
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

            Assert.False(config.HasConfigChanged(route, cluster));
        }

        [Fact]
        public void BuildEndpoints_DefaultAuth_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                AuthorizationPolicy = "defaulT",
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
            var attribute = Assert.IsType<AuthorizeAttribute>(routeEndpoint.Metadata.GetMetadata<IAuthorizeData>());
            Assert.Null(attribute.Policy);
        }

        [Fact]
        public void BuildEndpoints_CustomAuth_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                AuthorizationPolicy = "custom",
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
            var attribute = Assert.IsType<AuthorizeAttribute>(routeEndpoint.Metadata.GetMetadata<IAuthorizeData>());
            Assert.Equal("custom", attribute.Policy);
        }

        [Fact]
        public void BuildEndpoints_NoAuth_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
            Assert.Null(routeEndpoint.Metadata.GetMetadata<IAuthorizeData>());
            Assert.Null(routeEndpoint.Metadata.GetMetadata<IAllowAnonymous>());
        }

        [Fact]
        public void BuildEndpoints_DefaultCors_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                CorsPolicy = "defaulT",
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
            var attribute = Assert.IsType<EnableCorsAttribute>(routeEndpoint.Metadata.GetMetadata<IEnableCorsAttribute>());
            Assert.Null(attribute.PolicyName);
            Assert.Null(routeEndpoint.Metadata.GetMetadata<IDisableCorsAttribute>());
        }

        [Fact]
        public void BuildEndpoints_CustomCors_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                CorsPolicy = "custom",
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
            var attribute = Assert.IsType<EnableCorsAttribute>(routeEndpoint.Metadata.GetMetadata<IEnableCorsAttribute>());
            Assert.Equal("custom", attribute.PolicyName);
            Assert.Null(routeEndpoint.Metadata.GetMetadata<IDisableCorsAttribute>());
        }

        [Fact]
        public void BuildEndpoints_DisableCors_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                CorsPolicy = "disAble",
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
            Assert.IsType<DisableCorsAttribute>(routeEndpoint.Metadata.GetMetadata<IDisableCorsAttribute>());
            Assert.Null(routeEndpoint.Metadata.GetMetadata<IEnableCorsAttribute>());
        }

        [Fact]
        public void BuildEndpoints_NoCors_Works()
        {
            var services = CreateServices();
            var builder = services.GetRequiredService<IRuntimeRouteBuilder>();
            builder.SetProxyPipeline(context => Task.CompletedTask);

            var route = new ProxyRoute
            {
                RouteId = "route1",
                Order = 12,
            };
            var cluster = new ClusterInfo("cluster1", new DestinationManager(null));
            var routeInfo = new RouteInfo("route1");

            var config = builder.Build(route, cluster, routeInfo);

            Assert.Single(config.Endpoints);
            var routeEndpoint = config.Endpoints[0] as AspNetCore.Routing.RouteEndpoint;
            Assert.Null(routeEndpoint.Metadata.GetMetadata<IEnableCorsAttribute>());
            Assert.Null(routeEndpoint.Metadata.GetMetadata<IDisableCorsAttribute>());
        }
    }
}

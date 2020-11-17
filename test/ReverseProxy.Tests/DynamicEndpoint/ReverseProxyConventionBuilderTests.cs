using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Xunit;

namespace Microsoft.ReverseProxy.DynamicEndpoint
{
    public class ReverseProxyConventionBuilderTests
    {
        [Fact]
        public void ReverseProxyConventionBuilder_Configure_Works()
        {
            var configured = false;

            var conventions = new List<Action<EndpointBuilder>>();
            var builder = new ReverseProxyConventionBuilder(conventions);

            builder.Configure(builder =>
            {
                configured = true;
            });

            var proxyRoute = new ProxyRoute();
            var cluster = new Cluster();
            var endpointBuilder = CreateEndpointBuilder(proxyRoute, cluster);

            var action = Assert.Single(conventions);
            action(endpointBuilder);

            Assert.True(configured);
        }

        [Fact]
        public void ReverseProxyConventionBuilder_ConfigureWithProxy_Works()
        {
            var configured = false;

            var conventions = new List<Action<EndpointBuilder>>();
            var builder = new ReverseProxyConventionBuilder(conventions);

            builder.Configure((builder, proxy) =>
            {
                configured = true;
            });

            var proxyRoute = new ProxyRoute();
            var cluster = new Cluster();
            var endpointBuilder = CreateEndpointBuilder(proxyRoute, cluster);

            var action = Assert.Single(conventions);
            action(endpointBuilder);

            Assert.True(configured);
        }

        [Fact]
        public void ReverseProxyConventionBuilder_ConfigureWithProxyAndCluster_Works()
        {
            var configured = false;

            var conventions = new List<Action<EndpointBuilder>>();
            var builder = new ReverseProxyConventionBuilder(conventions);

            builder.Configure((builder, proxy, cluster) =>
            {
                configured = true;
            });

            var proxyRoute = new ProxyRoute();
            var cluster = new Cluster();
            var endpointBuilder = CreateEndpointBuilder(proxyRoute, cluster);

            var action = Assert.Single(conventions);
            action(endpointBuilder);

            Assert.True(configured);
        }

        private static RouteEndpointBuilder CreateEndpointBuilder(ProxyRoute proxyRoute, Cluster cluster)
        {
            var endpointBuilder = new RouteEndpointBuilder(context => Task.CompletedTask, RoutePatternFactory.Parse(""), 0);
            var routeConfig = new RouteConfig(
                new RouteInfo("route-1"),
                proxyRoute,
                new ClusterInfo("cluster-1", new DestinationManager())
                {
                    Config = new ClusterConfig(cluster, default, default, default, default, default, default, default)
                },
                Transforms.Empty);

            endpointBuilder.Metadata.Add(routeConfig);

            return endpointBuilder;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service.Management;
using Yarp.ReverseProxy.Service.Proxy;

namespace Yarp.ReverseProxy.DynamicEndpoint
{
    public class ReverseProxyConventionBuilderTests
    {
        [Fact]
        public void ReverseProxyConventionBuilder_Configure_Works()
        {
            var configured = false;

            var conventions = new List<Action<EndpointBuilder>>();
            var builder = new ReverseProxyConventionBuilder(conventions);

            builder.ConfigureEndpoints(builder =>
            {
                configured = true;
            });

            var proxyRoute = new RouteConfig();
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

            builder.ConfigureEndpoints((builder, proxy) =>
            {
                configured = true;
            });

            var proxyRoute = new RouteConfig();
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

            builder.ConfigureEndpoints((builder, proxy, cluster) =>
            {
                configured = true;
            });

            var proxyRoute = new RouteConfig();
            var cluster = new Cluster();
            var endpointBuilder = CreateEndpointBuilder(proxyRoute, cluster);

            var action = Assert.Single(conventions);
            action(endpointBuilder);

            Assert.True(configured);
        }

        private static RouteEndpointBuilder CreateEndpointBuilder(RouteConfig proxyRoute, Cluster cluster)
        {
            var endpointBuilder = new RouteEndpointBuilder(context => Task.CompletedTask, RoutePatternFactory.Parse(""), 0);
            var routeConfig = new RouteModel(
                proxyRoute,
                new ClusterInfo("cluster-1")
                {
                    Config = new ClusterConfig(cluster, default)
                },
                HttpTransformer.Default);

            endpointBuilder.Metadata.Add(routeConfig);

            return endpointBuilder;
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.HealthChecks;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Middleware
{
    public class PassiveHealthCheckMiddlewareTests
    {
        [Fact]
        public async Task Invoke_PassiveHealthCheckIsEnabled_CallPolicy()
        {
            var policies = new[] { GetPolicy("policy0"), GetPolicy("policy1") };
            var cluster0 = GetClusterInfo("cluster0", "policy0");
            var cluster1 = GetClusterInfo("cluster1", "policy1");
            var nextInvoked = false;
            var middleware = new PassiveHealthCheckMiddleware(c => {
                nextInvoked = true;
                return Task.CompletedTask;
            }, policies.Select(p => p.Object));

            var context0 = GetContext(cluster0, selectedDestination: 1, error: null);
            await middleware.Invoke(context0);

            Assert.True(nextInvoked);
            policies[0].Verify(p => p.RequestProxied(cluster0, cluster0.DynamicState.AllDestinations[1], context0), Times.Once);
            policies[0].VerifyGet(p => p.Name, Times.Once);
            policies[0].VerifyNoOtherCalls();
            policies[1].VerifyGet(p => p.Name, Times.Once);
            policies[1].VerifyNoOtherCalls();

            nextInvoked = false;

            var error = new ProxyErrorFeature(ProxyError.Request, null);
            var context1 = GetContext(cluster1, selectedDestination: 0, error);
            await middleware.Invoke(context1);

            Assert.True(nextInvoked);
            policies[1].Verify(p => p.RequestProxied(cluster1, cluster1.DynamicState.AllDestinations[0], context1), Times.Once);
            policies[1].VerifyNoOtherCalls();
            policies[0].VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Invoke_PassiveHealthCheckIsDisabled_DoNothing()
        {
            var policies = new[] { GetPolicy("policy0"), GetPolicy("policy1") };
            var cluster0 = GetClusterInfo("cluster0", "policy0", enabled: false);
            var nextInvoked = false;
            var middleware = new PassiveHealthCheckMiddleware(c => {
                nextInvoked = true;
                return Task.CompletedTask;
            }, policies.Select(p => p.Object));

            var context0 = GetContext(cluster0, selectedDestination: 0, error: null);
            await middleware.Invoke(context0);

            Assert.True(nextInvoked);
            policies[0].VerifyGet(p => p.Name, Times.Once);
            policies[0].VerifyNoOtherCalls();
            policies[1].VerifyGet(p => p.Name, Times.Once);
            policies[1].VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Invoke_PassiveHealthCheckIsEnabledButNoDestinationSelected_DoNothing()
        {
            var policies = new[] { GetPolicy("policy0"), GetPolicy("policy1") };
            var cluster0 = GetClusterInfo("cluster0", "policy0");
            var nextInvoked = false;
            var middleware = new PassiveHealthCheckMiddleware(c => {
                nextInvoked = true;
                return Task.CompletedTask;
            }, policies.Select(p => p.Object));

            var context0 = GetContext(cluster0, selectedDestination: 1, error: null);
            context0.GetRequiredProxyFeature().SelectedDestination = null;
            await middleware.Invoke(context0);

            Assert.True(nextInvoked);
            policies[0].VerifyGet(p => p.Name, Times.Once);
            policies[0].VerifyNoOtherCalls();
            policies[1].VerifyGet(p => p.Name, Times.Once);
            policies[1].VerifyNoOtherCalls();
        }

        private HttpContext GetContext(ClusterInfo cluster, int selectedDestination, IProxyErrorFeature error)
        {
            var context = new DefaultHttpContext();
            context.Features.Set(GetProxyFeature(cluster.Config, cluster.DynamicState.AllDestinations[selectedDestination]));
            context.Features.Set(error);
            context.SetEndpoint(GetEndpoint(cluster));
            return context;
        }

        private Mock<IPassiveHealthCheckPolicy> GetPolicy(string name)
        {
            var policy = new Mock<IPassiveHealthCheckPolicy>();
            policy.SetupGet(p => p.Name).Returns(name);
            return policy;
        }

        private IReverseProxyFeature GetProxyFeature(ClusterConfig clusterConfig, DestinationInfo destination)
        {
            var result = new Mock<IReverseProxyFeature>(MockBehavior.Strict);
            result.SetupProperty(p => p.SelectedDestination, destination);
            result.SetupProperty(p => p.ClusterConfig, clusterConfig);
            return result.Object;
        }

        private ClusterInfo GetClusterInfo(string id, string policy, bool enabled = true)
        {
            var clusterConfig = new ClusterConfig(
                new Cluster { Id = id },
                new ClusterHealthCheckOptions(new ClusterPassiveHealthCheckOptions(enabled, policy, null), default),
                default,
                default,
                null,
                default,
                default,
                null);
            var clusterInfo = new ClusterInfo(id, new DestinationManager());
            clusterInfo.ConfigSignal.Value = clusterConfig;
            clusterInfo.DestinationManager.GetOrCreateItem("destination0", d => { });
            clusterInfo.DestinationManager.GetOrCreateItem("destination1", d => { });

            return clusterInfo;
        }

        private Endpoint GetEndpoint(ClusterInfo cluster)
        {
            var endpoints = new List<Endpoint>(1);
            var routeConfig = new RouteConfig(new RouteInfo("route-1"), new ProxyRoute(), cluster, endpoints.AsReadOnly(), Transforms.Empty);
            var endpoint = new Endpoint(default, new EndpointMetadataCollection(routeConfig), string.Empty);
            endpoints.Add(endpoint);
            return endpoint;
        }
    }
}

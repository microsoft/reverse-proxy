// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service.HealthChecks;
using Yarp.ReverseProxy.Service.Management;
using Yarp.ReverseProxy.Service.Proxy;

namespace Yarp.ReverseProxy.Middleware
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
            context0.GetRequiredProxyFeature().ProxiedDestination = null;
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
            context.Features.Set(GetProxyFeature(cluster, cluster.DynamicState.AllDestinations[selectedDestination]));
            context.Features.Set(error);
            return context;
        }

        private Mock<IPassiveHealthCheckPolicy> GetPolicy(string name)
        {
            var policy = new Mock<IPassiveHealthCheckPolicy>();
            policy.SetupGet(p => p.Name).Returns(name);
            return policy;
        }

        private IReverseProxyFeature GetProxyFeature(ClusterInfo clusterInfo, DestinationInfo destination)
        {
            return new ReverseProxyFeature()
            {
                ProxiedDestination = destination,
                ClusterSnapshot = clusterInfo.Config,
                RouteSnapshot = new RouteConfig(new ProxyRoute(), clusterInfo, HttpTransformer.Default),
            };
        }

        private ClusterInfo GetClusterInfo(string id, string policy, bool enabled = true)
        {
            var clusterConfig = new ClusterConfig(
                new Cluster
                {
                    Id = id,
                    HealthCheck = new HealthCheckOptions
                    {
                        Passive = new PassiveHealthCheckOptions
                        {
                            Enabled = enabled,
                            Policy = policy,
                        }
                    }
                },
                null);
            var clusterInfo = new ClusterInfo(id);
            clusterInfo.Config = clusterConfig;
            clusterInfo.DestinationManager.GetOrCreateItem("destination0", d => { });
            clusterInfo.DestinationManager.GetOrCreateItem("destination1", d => { });

            clusterInfo.UpdateDynamicState();

            return clusterInfo;
        }
    }
}

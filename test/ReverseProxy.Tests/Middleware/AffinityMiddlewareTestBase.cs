// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Microsoft.ReverseProxy.Service.SessionAffinity;
using Moq;

namespace Microsoft.ReverseProxy.Middleware
{
    public abstract class AffinityMiddlewareTestBase
    {
        protected const string AffinitizedDestinationName = "dest-B";
        protected readonly ClusterConfig ClusterConfig = new ClusterConfig(new Cluster(), default, new ClusterSessionAffinityOptions(true, "Mode-B", "Policy-1", null),
            new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object), default, default, new Dictionary<string, string>());

        internal ClusterInfo GetCluster()
        {
            var destinationManager = new DestinationManager();
            destinationManager.GetOrCreateItem("dest-A", d => { });
            destinationManager.GetOrCreateItem(AffinitizedDestinationName, d => { });
            destinationManager.GetOrCreateItem("dest-C", d => { });

            var cluster = new ClusterInfo("cluster-1", destinationManager);
            cluster.Config = ClusterConfig;
            cluster.UpdateDynamicState();
            return cluster;
        }

        internal IReadOnlyList<Mock<ISessionAffinityProvider>> RegisterAffinityProviders(
            bool lookupMiddlewareTest,
            IReadOnlyList<DestinationInfo> expectedDestinations,
            string expectedCluster,
            params (string Mode, AffinityStatus? Status, DestinationInfo[] Destinations, Action<ISessionAffinityProvider> Callback)[] prototypes)
        {
            var result = new List<Mock<ISessionAffinityProvider>>();
            foreach (var (mode, status, destinations, callback) in prototypes)
            {
                var provider = new Mock<ISessionAffinityProvider>(MockBehavior.Strict);
                provider.SetupGet(p => p.Mode).Returns(mode);
                if (lookupMiddlewareTest)
                {
                    provider.Setup(p => p.FindAffinitizedDestinations(
                        It.IsAny<HttpContext>(),
                        expectedDestinations,
                        expectedCluster,
                        ClusterConfig.SessionAffinityOptions))
                    .Returns(new AffinityResult(destinations, status.Value))
                    .Callback(() => callback(provider.Object));
                }
                else
                {
                    provider.Setup(p => p.AffinitizeRequest(
                        It.IsAny<HttpContext>(),
                        ClusterConfig.SessionAffinityOptions,
                        expectedDestinations[0]))
                    .Callback(() => callback(provider.Object));
                }
                result.Add(provider);
            }
            return result.AsReadOnly();
        }

        internal IReadOnlyList<Mock<IAffinityFailurePolicy>> RegisterFailurePolicies(AffinityStatus expectedStatus, params (string Name, bool Handled, Action<IAffinityFailurePolicy> Callback)[] prototypes)
        {
            var result = new List<Mock<IAffinityFailurePolicy>>();
            foreach (var (name, handled, callback) in prototypes)
            {
                var policy = new Mock<IAffinityFailurePolicy>(MockBehavior.Strict);
                policy.SetupGet(p => p.Name).Returns(name);
                policy.Setup(p => p.Handle(It.IsAny<HttpContext>(), It.Is<ClusterSessionAffinityOptions>(o => o.FailurePolicy == name), expectedStatus))
                    .ReturnsAsync(handled)
                    .Callback(() => callback(policy.Object));
                result.Add(policy);
            }
            return result.AsReadOnly();
        }

        internal IReverseProxyFeature GetDestinationsFeature(IReadOnlyList<DestinationInfo> destinations, ClusterConfig clusterConfig)
        {
            var result = new Mock<IReverseProxyFeature>(MockBehavior.Strict);
            result.SetupProperty(p => p.AvailableDestinations, destinations);
            result.SetupProperty(p => p.ClusterConfig, clusterConfig);
            return result.Object;
        }

        internal Endpoint GetEndpoint(ClusterInfo cluster)
        {
            var proxyRoute = new ProxyRoute();
            var routeConfig = new RouteConfig(new RouteInfo("route-1"), proxyRoute, cluster, HttpTransformer.Default);
            var endpoint = new Endpoint(default, new EndpointMetadataCollection(routeConfig), string.Empty);
            return endpoint;
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Moq;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service.Management;
using Yarp.ReverseProxy.Service.Proxy;
using Yarp.ReverseProxy.Service.SessionAffinity;

namespace Yarp.ReverseProxy.Middleware
{
    public abstract class AffinityMiddlewareTestBase
    {
        protected const string AffinitizedDestinationName = "dest-B";
        protected readonly ClusterConfig ClusterConfig = new ClusterConfig(new Cluster
            {
                Id = "cluster-1",
                SessionAffinity = new SessionAffinityOptions
                {
                    Enabled = true,
                    Mode = "Mode-B",
                    FailurePolicy = "Policy-1",
                }
            },
            new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));

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
                        ClusterConfig))
                    .Returns(new AffinityResult(destinations, status.Value))
                    .Callback(() => callback(provider.Object));
                }
                else
                {
                    provider.Setup(p => p.AffinitizeRequest(
                        It.IsAny<HttpContext>(),
                        ClusterConfig,
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
                policy.Setup(p => p.Handle(It.IsAny<HttpContext>(), It.Is<SessionAffinityOptions>(o => o.FailurePolicy == name), expectedStatus))
                    .ReturnsAsync(handled)
                    .Callback(() => callback(policy.Object));
                result.Add(policy);
            }
            return result.AsReadOnly();
        }

        internal IReverseProxyFeature GetDestinationsFeature(IReadOnlyList<DestinationInfo> destinations, ClusterConfig clusterConfig)
        {
            return new ReverseProxyFeature()
            {
                AvailableDestinations = destinations,
                ClusterSnapshot = clusterConfig,
            };
        }

        internal Endpoint GetEndpoint(ClusterInfo cluster)
        {
            var proxyRoute = new ProxyRoute();
            var routeConfig = new RouteConfig(proxyRoute, cluster, HttpTransformer.Default);
            var endpoint = new Endpoint(default, new EndpointMetadataCollection(routeConfig), string.Empty);
            return endpoint;
        }
    }
}

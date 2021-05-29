// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Discovery;
using Yarp.ReverseProxy.Middleware;
using Yarp.ReverseProxy.RuntimeModel;
using Yarp.ReverseProxy.Service.Management;
using Yarp.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Yarp.ReverseProxy.Service.SessionAffinity
{
    public class AffinitizeTransformTests
    {
        [Fact]
        public async Task ApplyAsync_InvokeAffinitizeRequest()
        {
            var cluster = GetCluster();
            var destination = cluster.Destinations.Values.First();
            var provider = new Mock<ISessionAffinityProvider>(MockBehavior.Strict);
            provider.Setup(p => p.AffinitizeRequest(It.IsAny<HttpContext>(), It.IsNotNull<SessionAffinityConfig>(), It.IsAny<DestinationState>()));

            var transform = new AffinitizeTransform(provider.Object);

            var context = new DefaultHttpContext();
            context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature()
            {
                Cluster = cluster.Model,
                ProxiedDestination = destination,
            });

            var transformContext = new ResponseTransformContext()
            {
                HttpContext = context,
            };
            await transform.ApplyAsync(transformContext);

            provider.Verify();
        }

        internal ClusterState GetCluster()
        {
            var cluster = new ClusterState("cluster-1");
            cluster.Destinations.GetOrAdd("dest-A", id => new DestinationState(id));
            cluster.Model = new ClusterModel(new ClusterConfig
            {
                SessionAffinity = new SessionAffinityConfig
                {
                    Enabled = true,
                    Mode = "Mode-B",
                    FailurePolicy = "Policy-1",
                    AffinityKeyName = "Key1"
                }
            },
            new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));

            var destinations = cluster.Destinations.Values.ToList();
            cluster.DestinationsState = new ClusterDestinationsState(destinations, destinations);
            return cluster;
        }
    }
}

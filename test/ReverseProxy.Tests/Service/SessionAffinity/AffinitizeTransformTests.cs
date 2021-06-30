// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
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
            provider.Setup(p => p.AffinitizeRequest(It.IsAny<HttpContext>(), It.IsNotNull<SessionAffinityOptions>(), It.IsAny<DestinationInfo>()));

            var transform = new AffinitizeTransform(provider.Object);

            var context = new DefaultHttpContext();
            context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature()
            {
                ClusterSnapshot = cluster.Config,
                ProxiedDestination = destination,
            });

            var transformContext = new ResponseTransformContext()
            {
                HttpContext = context,
            };
            await transform.ApplyAsync(transformContext);

            provider.Verify();
        }

        internal ClusterInfo GetCluster()
        {
            var cluster = new ClusterInfo("cluster-1");
            cluster.Destinations.GetOrAdd("dest-A", id => new DestinationInfo(id));
            cluster.Config = new ClusterConfig(new Cluster
            {
                SessionAffinity = new SessionAffinityOptions
                {
                    Enabled = true,
                    Mode = "Mode-B",
                    FailurePolicy = "Policy-1",
                }
            },
            new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));

            cluster.ProcessDestinationChanges();
            return cluster;
        }
    }
}

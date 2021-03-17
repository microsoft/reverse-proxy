// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            var destination = cluster.DestinationManager.Items[0];
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
            var destinationManager = new DestinationManager();
            destinationManager.GetOrCreateItem("dest-A", d => { });

            var cluster = new ClusterInfo("cluster-1", destinationManager);
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

            cluster.UpdateDynamicState();
            return cluster;
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Xunit;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    public class DefaultProbingRequestFactoryTests
    {
        [Theory]
        [InlineData("https://localhost:10000/", null, null, "https://localhost:10000/")]
        [InlineData("https://localhost:10000/", "https://localhost:20000/", null, "https://localhost:20000/")]
        [InlineData("https://localhost:10000/", null, "/api/health/", "https://localhost:10000/api/health/")]
        [InlineData("https://localhost:10000/", "https://localhost:20000/", "/api/health/", "https://localhost:20000/api/health/")]
        public void CreateRequest_HealthEndpointIsNotDefined_UseDestinationAddress(string address, string health, string healthPath, string expectedRequestUri)
        {
            var clusterConfig = GetClusterConfig("cluster0", new ClusterConfig.ClusterActiveHealthCheckOptions(true, null, null, "policy", healthPath));
            var destinationConfig = new DestinationConfig(address, health);
            var factory = new DefaultProbingRequestFactory();

            var request = factory.CreateRequest(clusterConfig, destinationConfig);

            Assert.Equal(expectedRequestUri, request.RequestUri.AbsoluteUri);
            Assert.Equal(ProtocolHelper.Http2Version, request.Version);
        }

        private ClusterConfig GetClusterConfig(string id, ClusterConfig.ClusterActiveHealthCheckOptions healthCheckOptions)
        {
            return new ClusterConfig(
                new Cluster { Id = id }, new ClusterConfig.ClusterHealthCheckOptions(default, healthCheckOptions), default, default, null, default, null);
        }
    }
}

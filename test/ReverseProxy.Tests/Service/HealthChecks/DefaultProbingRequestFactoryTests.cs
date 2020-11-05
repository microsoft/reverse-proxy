// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
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
        [InlineData("https://localhost:10000/api", "https://localhost:20000/", "/health/", "https://localhost:20000/health/")]
        [InlineData("https://localhost:10000/", "https://localhost:20000/api", "/health/", "https://localhost:20000/api/health/")]
        public void CreateRequest_HealthEndpointIsNotDefined_UseDestinationAddress(string address, string health, string healthPath, string expectedRequestUri)
        {
            var version = HttpVersion.Version11;
            var clusterConfig = GetClusterConfig("cluster0", new ClusterActiveHealthCheckOptions(true, null, null, "policy", healthPath), version);
            var destinationConfig = new DestinationConfig(address, health);
            var factory = new DefaultProbingRequestFactory();

            var request = factory.CreateRequest(clusterConfig, destinationConfig);

            Assert.Equal(expectedRequestUri, request.RequestUri.AbsoluteUri);
            Assert.Equal(version, request.Version);
        }

        private ClusterConfig GetClusterConfig(string id, ClusterActiveHealthCheckOptions healthCheckOptions, Version version)
        {
            return new ClusterConfig(
                new Cluster { Id = id },
                new ClusterHealthCheckOptions(default, healthCheckOptions),
                default,
                default,
                null,
                default,
                new ClusterProxyHttpRequestOptions(
                    TimeSpan.FromSeconds(60),
                    version
#if NET
                    , HttpVersionPolicy.RequestVersionExact
#endif
                    ), null);
        }
    }
}

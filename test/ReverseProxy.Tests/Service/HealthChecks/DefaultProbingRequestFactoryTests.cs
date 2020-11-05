// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
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
            var clusterConfig = GetClusterConfig("cluster0", new ClusterActiveHealthCheckOptions(true, null, null, "policy", healthPath), HttpVersion.Version20);
            var destinationConfig = new DestinationConfig(address, health);
            var factory = new DefaultProbingRequestFactory();

            var request = factory.CreateRequest(clusterConfig, destinationConfig);

            Assert.Equal(expectedRequestUri, request.RequestUri.AbsoluteUri);
        }

        [Theory]
        [MemberData(nameof(RequestVersionMemberData))]
        public void CreateRequest_RequestVersionProperties(Version version
#if NET
            , HttpVersionPolicy versionPolicy
#endif
        )
        {
            var clusterConfig = GetClusterConfig("cluster0", new ClusterActiveHealthCheckOptions(true, null, null, "policy", null), version
#if NET
                , versionPolicy
#endif
                );
            var destinationConfig = new DestinationConfig("https://localhost:10000/", null);
            var factory = new DefaultProbingRequestFactory();

            var request = factory.CreateRequest(clusterConfig, destinationConfig);

            Assert.Equal(version ?? HttpVersion.Version20, request.Version);
#if NET
            Assert.Equal(versionPolicy, request.VersionPolicy);
#endif
        }

        public static IEnumerable<object[]> RequestVersionMemberData() =>
            from version in new[] { HttpVersion.Version10, HttpVersion.Version11, HttpVersion.Version20, null }
#if NET
            from policy in new [] { HttpVersionPolicy.RequestVersionExact, HttpVersionPolicy.RequestVersionOrHigher, HttpVersionPolicy.RequestVersionOrLower }
#endif
            select new object[] {
                version
#if NET
                , policy
#endif
            };

        private ClusterConfig GetClusterConfig(string id, ClusterActiveHealthCheckOptions healthCheckOptions, Version version
#if NET
            , HttpVersionPolicy versionPolicy = HttpVersionPolicy.RequestVersionExact
#endif
            )
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
                    , versionPolicy
#endif
                    ), null);
        }
    }
}

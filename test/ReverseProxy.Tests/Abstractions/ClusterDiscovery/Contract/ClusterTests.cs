// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
{
    public class ClusterTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new Cluster();
        }

        [Fact]
        public void DeepClone_Works()
        {
            var cluster = new Cluster
            {
                LoadBalancing = new LoadBalancingOptions(),
                HealthCheck = new HealthCheckOptions(),
                HttpClient = new ProxyHttpClientOptions(),
                HttpRequest = new ProxyHttpRequestOptions(),
                Metadata = new Dictionary<string, string>
                {
                    { "key", "value" }
                }
            };

            var clone = cluster.DeepClone();

            Assert.NotSame(cluster, clone);
            Assert.NotNull(clone.LoadBalancing);
            Assert.NotSame(cluster.LoadBalancing, clone.LoadBalancing);
            Assert.NotNull(clone.HealthCheck);
            Assert.NotSame(cluster.HealthCheck, clone.HealthCheck);
            Assert.NotNull(clone.HttpClient);
            Assert.NotSame(cluster.HttpClient, clone.HttpClient);
            Assert.NotNull(clone.Metadata);
            Assert.NotSame(cluster.Metadata, clone.Metadata);
            Assert.Equal("value", clone.Metadata["key"]);
        }

        [Fact]
        public void DeepClone_Nulls_Works()
        {
            var cluster = new Cluster();

            var clone = cluster.DeepClone();

            Assert.NotSame(cluster, clone);
            Assert.Null(clone.LoadBalancing);
            Assert.Null(clone.HealthCheck);
            Assert.Null(clone.HttpClient);
            Assert.Null(clone.Metadata);
        }
    }
}

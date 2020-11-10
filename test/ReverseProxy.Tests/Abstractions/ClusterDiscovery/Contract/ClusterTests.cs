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
            // Arrange
            var cluster = new Cluster
            {
                CircuitBreaker = new CircuitBreakerOptions(),
                Quota = new QuotaOptions(),
                Partitioning = new ClusterPartitioningOptions(),
                LoadBalancing = new LoadBalancingOptions(),
                HealthCheck = new HealthCheckOptions(),
                HttpClient = new ProxyHttpClientOptions(),
                HttpRequest = new ProxyHttpRequestOptions(),
                Metadata = new Dictionary<string, string>
                {
                    { "key", "value" },
                },
            };

            // Act
            var clone = cluster.DeepClone();

            // Assert
            Assert.NotSame(cluster, clone);
            Assert.NotNull(clone.CircuitBreaker);
            Assert.NotSame(cluster.CircuitBreaker, clone.CircuitBreaker);
            Assert.NotNull(clone.Quota);
            Assert.NotSame(cluster.Quota, clone.Quota);
            Assert.NotNull(clone.Partitioning);
            Assert.NotSame(cluster.Partitioning, clone.Partitioning);
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
            // Arrange
            var cluster = new Cluster();

            // Act
            var clone = cluster.DeepClone();

            // Assert
            Assert.NotSame(cluster, clone);
            Assert.Null(clone.CircuitBreaker);
            Assert.Null(clone.Quota);
            Assert.Null(clone.Partitioning);
            Assert.Null(clone.LoadBalancing);
            Assert.Null(clone.HealthCheck);
            Assert.Null(clone.HttpClient);
            Assert.Null(clone.Metadata);
        }
    }
}

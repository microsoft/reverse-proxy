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
                CircuitBreakerOptions = new CircuitBreakerOptions(),
                QuotaOptions = new QuotaOptions(),
                PartitioningOptions = new ClusterPartitioningOptions(),
                LoadBalancing = new LoadBalancingOptions(),
                HealthCheckOptions = new HealthCheckOptions(),
                Metadata = new Dictionary<string, string>
                {
                    { "key", "value" },
                },
            };

            // Act
            var clone = cluster.DeepClone();

            // Assert
            Assert.NotSame(cluster, clone);
            Assert.NotNull(clone.CircuitBreakerOptions);
            Assert.NotSame(cluster.CircuitBreakerOptions, clone.CircuitBreakerOptions);
            Assert.NotNull(clone.QuotaOptions);
            Assert.NotSame(cluster.QuotaOptions, clone.QuotaOptions);
            Assert.NotNull(clone.PartitioningOptions);
            Assert.NotSame(cluster.PartitioningOptions, clone.PartitioningOptions);
            Assert.NotNull(clone.LoadBalancing);
            Assert.NotSame(cluster.LoadBalancing, clone.LoadBalancing);
            Assert.NotNull(clone.HealthCheckOptions);
            Assert.NotSame(cluster.HealthCheckOptions, clone.HealthCheckOptions);
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
            Assert.Null(clone.CircuitBreakerOptions);
            Assert.Null(clone.QuotaOptions);
            Assert.Null(clone.PartitioningOptions);
            Assert.Null(clone.LoadBalancing);
            Assert.Null(clone.HealthCheckOptions);
            Assert.Null(clone.Metadata);
        }
    }
}

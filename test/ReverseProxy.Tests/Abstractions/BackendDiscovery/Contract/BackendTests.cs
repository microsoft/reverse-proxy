// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
{
    public class BackendTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new Backend();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var backend = new Backend
            {
                CircuitBreakerOptions = new CircuitBreakerOptions(),
                QuotaOptions = new QuotaOptions(),
                PartitioningOptions = new BackendPartitioningOptions(),
                LoadBalancing = new LoadBalancingOptions(),
                HealthCheckOptions = new HealthCheckOptions(),
                Metadata = new Dictionary<string, string>
                {
                    { "key", "value" },
                },
            };

            // Act
            var clone = backend.DeepClone();

            // Assert
            Assert.NotSame(backend, clone);
            Assert.NotNull(clone.CircuitBreakerOptions);
            Assert.NotSame(backend.CircuitBreakerOptions, clone.CircuitBreakerOptions);
            Assert.NotNull(clone.QuotaOptions);
            Assert.NotSame(backend.QuotaOptions, clone.QuotaOptions);
            Assert.NotNull(clone.PartitioningOptions);
            Assert.NotSame(backend.PartitioningOptions, clone.PartitioningOptions);
            Assert.NotNull(clone.LoadBalancing);
            Assert.NotSame(backend.LoadBalancing, clone.LoadBalancing);
            Assert.Null(clone.HealthCheckOptions);
            Assert.NotSame(backend.HealthCheckOptions, clone.HealthCheckOptions);
            Assert.NotNull(clone.Metadata);
            Assert.NotSame(backend.Metadata, clone.Metadata);
            Assert.Equal("value", clone.Metadata["key"]);
        }

        [Fact]
        public void DeepClone_Nulls_Works()
        {
            // Arrange
            var backend = new Backend();

            // Act
            var clone = backend.DeepClone();

            // Assert
            Assert.NotSame(backend, clone);
            Assert.Null(clone.CircuitBreakerOptions);
            Assert.Null(clone.QuotaOptions);
            Assert.Null(clone.PartitioningOptions);
            Assert.Null(clone.LoadBalancing);
            Assert.Null(clone.HealthCheckOptions);
            Assert.Null(clone.Metadata);
        }
    }
}

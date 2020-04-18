// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Abstractions.Tests
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
                LoadBalancingOptions = new LoadBalancingOptions(),
                HealthCheckOptions = new HealthCheckOptions(),
                Metadata = new Dictionary<string, string>
                {
                    { "key", "value" },
                },
            };

            // Act
            var clone = backend.DeepClone();

            // Assert
            Assert.NotEqual(backend, clone);
            Assert.NotNull(clone.CircuitBreakerOptions);
            Assert.NotEqual(backend.CircuitBreakerOptions, clone.CircuitBreakerOptions);
            Assert.NotNull(clone.QuotaOptions);
            Assert.NotEqual(backend.QuotaOptions, clone.QuotaOptions);
            Assert.NotNull(clone.PartitioningOptions);
            Assert.NotEqual(backend.PartitioningOptions, clone.PartitioningOptions);
            Assert.NotNull(clone.LoadBalancingOptions);
            Assert.NotEqual(backend.LoadBalancingOptions, clone.LoadBalancingOptions);
            Assert.NotNull(clone.HealthCheckOptions);
            Assert.NotEqual(backend.HealthCheckOptions, clone.HealthCheckOptions);
            Assert.NotNull(clone.Metadata);
            Assert.NotStrictEqual(backend.Metadata, clone.Metadata);
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
            Assert.NotEqual(backend, clone);
            Assert.Null(clone.CircuitBreakerOptions);
            Assert.Null(clone.QuotaOptions);
            Assert.Null(clone.PartitioningOptions);
            Assert.Null(clone.LoadBalancingOptions);
            Assert.Null(clone.HealthCheckOptions);
            Assert.Null(clone.Metadata);
        }
    }
}

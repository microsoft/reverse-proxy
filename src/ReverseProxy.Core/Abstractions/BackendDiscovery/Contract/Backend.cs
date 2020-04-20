// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Core.Abstractions
{
    /// <summary>
    /// A backend is a group of equivalent endpoints and associated policies.
    /// A route maps requests to a backend, and Reverse Proxy handles that request
    /// by proxying to any endpoint within the matching backend,
    /// honoring load balancing and partitioning policies when applicable.
    /// </summary>
    public sealed class Backend : IDeepCloneable<Backend>
    {
        /// <summary>
        /// Circuit breaker options.
        /// </summary>
        public CircuitBreakerOptions CircuitBreakerOptions { get; set; }

        /// <summary>
        /// Quota options.
        /// </summary>
        public QuotaOptions QuotaOptions { get; set; }

        /// <summary>
        /// Partitioning options.
        /// </summary>
        public BackendPartitioningOptions PartitioningOptions { get; set; }

        /// <summary>
        /// Load balancing options.
        /// </summary>
        public LoadBalancingOptions LoadBalancing { get; set; }

        /// <summary>
        /// Active health checking options.
        /// </summary>
        public HealthCheckOptions HealthCheckOptions { get; set; }

        /// <summary>
        /// The set of backend endpoints associated with this backend.
        /// </summary>
        public IDictionary<string, BackendEndpoint> Endpoints { get; private set; } = new Dictionary<string, BackendEndpoint>(StringComparer.Ordinal);

        /// <summary>
        /// Arbitrary key-value pairs that further describe this backend.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }

        /// <inheritdoc/>
        Backend IDeepCloneable<Backend>.DeepClone()
        {
            return new Backend
            {
                CircuitBreakerOptions = CircuitBreakerOptions?.DeepClone(),
                QuotaOptions = QuotaOptions?.DeepClone(),
                PartitioningOptions = PartitioningOptions?.DeepClone(),
                LoadBalancing = LoadBalancing?.DeepClone(),
                HealthCheckOptions = HealthCheckOptions?.DeepClone(),
                Endpoints = Endpoints.DeepClone(StringComparer.Ordinal),
                Metadata = Metadata?.DeepClone(StringComparer.Ordinal),
            };
        }
    }
}

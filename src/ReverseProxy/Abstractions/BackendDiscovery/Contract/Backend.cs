// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;

namespace Microsoft.ReverseProxy.Abstractions
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
        /// The Id for this backend. This needs to be globally unique.
        /// </summary>
        public string Id { get; set; }

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
        /// Session affinity options.
        /// </summary>
        public SessionAffinityOptions SessionAffinity { get; set; }

        /// <summary>
        /// Active health checking options.
        /// </summary>
        public HealthCheckOptions HealthCheckOptions { get; set; }

        /// <summary>
        /// The set of destinations associated with this backend.
        /// </summary>
        public IDictionary<string, Destination> Destinations { get; private set; } = new Dictionary<string, Destination>(StringComparer.Ordinal);

        /// <summary>
        /// Arbitrary key-value pairs that further describe this backend.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }

        /// <inheritdoc/>
        Backend IDeepCloneable<Backend>.DeepClone()
        {
            return new Backend
            {
                Id = Id,
                CircuitBreakerOptions = CircuitBreakerOptions?.DeepClone(),
                QuotaOptions = QuotaOptions?.DeepClone(),
                PartitioningOptions = PartitioningOptions?.DeepClone(),
                LoadBalancing = LoadBalancing?.DeepClone(),
                SessionAffinity = SessionAffinity?.DeepClone(),
                HealthCheckOptions = HealthCheckOptions?.DeepClone(),
                Destinations = Destinations.DeepClone(StringComparer.Ordinal),
                Metadata = Metadata?.DeepClone(StringComparer.Ordinal),
            };
        }
    }
}

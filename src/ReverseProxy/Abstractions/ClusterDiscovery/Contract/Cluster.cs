// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// A cluster is a group of equivalent endpoints and associated policies.
    /// A route maps requests to a cluster, and Reverse Proxy handles that request
    /// by proxying to any endpoint within the matching cluster,
    /// honoring load balancing and partitioning policies when applicable.
    /// </summary>
    public sealed class Cluster : IDeepCloneable<Cluster>
    {
        /// <summary>
        /// The Id for this cluster. This needs to be globally unique.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Circuit breaker options.
        /// </summary>
        public CircuitBreakerOptions CircuitBreaker { get; set; }

        /// <summary>
        /// Quota options.
        /// </summary>
        public QuotaOptions Quota { get; set; }

        /// <summary>
        /// Partitioning options.
        /// </summary>
        public ClusterPartitioningOptions Partitioning { get; set; }

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
        public HealthCheckOptions HealthCheck { get; set; }

        /// <summary>
        /// Options of an HTTP client that is used to call this cluster.
        /// </summary>
        public ProxyHttpClientOptions HttpClient { get; set; }

        /// <summary>
        /// The set of destinations associated with this cluster.
        /// </summary>
        public IDictionary<string, Destination> Destinations { get; private set; } = new Dictionary<string, Destination>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Arbitrary key-value pairs that further describe this cluster.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }

        /// <inheritdoc/>
        Cluster IDeepCloneable<Cluster>.DeepClone()
        {
            return new Cluster
            {
                Id = Id,
                CircuitBreaker = CircuitBreaker?.DeepClone(),
                Quota = Quota?.DeepClone(),
                Partitioning = Partitioning?.DeepClone(),
                LoadBalancing = LoadBalancing?.DeepClone(),
                SessionAffinity = SessionAffinity?.DeepClone(),
                HealthCheck = HealthCheck?.DeepClone(),
                HttpClient = HttpClient?.DeepClone(),
                Destinations = Destinations.DeepClone(StringComparer.OrdinalIgnoreCase),
                Metadata = Metadata?.DeepClone(StringComparer.OrdinalIgnoreCase),
            };
        }
    }
}

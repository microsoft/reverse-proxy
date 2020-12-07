// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Utilities;

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
        /// Load balancing policy.
        /// </summary>
        public string LoadBalancingPolicy { get; set; }

        /// <summary>
        /// Session affinity options.
        /// </summary>
        public SessionAffinityOptions SessionAffinity { get; set; }

        /// <summary>
        /// Health checking options.
        /// </summary>
        public HealthCheckOptions HealthCheck { get; set; }

        /// <summary>
        /// Options of an HTTP client that is used to call this cluster.
        /// </summary>
        public ProxyHttpClientOptions HttpClient { get; set; }

        /// <summary>
        /// Options of an outgoing HTTP request.
        /// </summary>
        public ProxyHttpRequestOptions HttpRequest { get; set; }

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
                LoadBalancingPolicy = LoadBalancingPolicy,
                SessionAffinity = SessionAffinity?.DeepClone(),
                HealthCheck = HealthCheck?.DeepClone(),
                HttpClient = HttpClient?.DeepClone(),
                HttpRequest = HttpRequest?.DeepClone(),
                Destinations = Destinations.DeepClone(StringComparer.OrdinalIgnoreCase),
                Metadata = Metadata?.DeepClone(StringComparer.OrdinalIgnoreCase),
            };
        }

        internal static bool Equals(Cluster cluster1, Cluster cluster2)
        {
            if (cluster1 == null && cluster2 == null)
            {
                return true;
            }

            if (cluster1 == null || cluster2 == null)
            {
                return false;
            }

            return string.Equals(cluster1.Id, cluster2.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(cluster1.LoadBalancingPolicy, cluster2.LoadBalancingPolicy, StringComparison.OrdinalIgnoreCase)
                && SessionAffinityOptions.Equals(cluster1.SessionAffinity, cluster2.SessionAffinity)
                && HealthCheckOptions.Equals(cluster1.HealthCheck, cluster2.HealthCheck)
                && ProxyHttpClientOptions.Equals(cluster1.HttpClient, cluster2.HttpClient)
                && ProxyHttpRequestOptions.Equals(cluster1.HttpRequest, cluster2.HttpRequest)
                && CaseInsensitiveEqualHelper.Equals(cluster1.Destinations, cluster2.Destinations, Destination.Equals)
                && CaseInsensitiveEqualHelper.Equals(cluster1.Metadata, cluster2.Metadata);
        }
    }
}

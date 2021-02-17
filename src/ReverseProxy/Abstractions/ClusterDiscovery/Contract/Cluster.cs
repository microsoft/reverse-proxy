// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// A cluster is a group of equivalent endpoints and associated policies.
    /// A route maps requests to a cluster, and Reverse Proxy handles that request
    /// by proxying to any endpoint within the matching cluster,
    /// honoring load balancing and partitioning policies when applicable.
    /// </summary>
    public sealed record Cluster
    {
        /// <summary>
        /// The Id for this cluster. This needs to be globally unique.
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// Load balancing policy.
        /// </summary>
        public string LoadBalancingPolicy { get; init; }

        /// <summary>
        /// Session affinity options.
        /// </summary>
        public SessionAffinityOptions SessionAffinity { get; init; }

        /// <summary>
        /// Health checking options.
        /// </summary>
        public HealthCheckOptions HealthCheck { get; init; }

        /// <summary>
        /// Options of an HTTP client that is used to call this cluster.
        /// </summary>
        public ProxyHttpClientOptions HttpClient { get; init; }

        /// <summary>
        /// Options of an outgoing HTTP request.
        /// </summary>
        public RequestProxyOptions HttpRequest { get; init; }

        /// <summary>
        /// The set of destinations associated with this cluster.
        /// </summary>
        public IReadOnlyDictionary<string, Destination> Destinations { get; init; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this cluster.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; }

        /// <inheritdoc />
        public bool Equals(Cluster other)
        {
            if (other == null)
            {
                return false;
            }

            return string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(LoadBalancingPolicy, other.LoadBalancingPolicy, StringComparison.OrdinalIgnoreCase)
                // CS0252 warning only shows up in VS https://github.com/dotnet/roslyn/issues/49302
                && SessionAffinity == other.SessionAffinity
                && HealthCheck == other.HealthCheck
                && HttpClient == other.HttpClient
                && HttpRequest == other.HttpRequest
                && CaseInsensitiveEqualHelper.Equals(Destinations, other.Destinations, (a, b) => a == b)
                && CaseInsensitiveEqualHelper.Equals(Metadata, other.Metadata);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(
                Id?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                LoadBalancingPolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                SessionAffinity,
                HealthCheck,
                HttpClient,
                HttpRequest,
                Destinations,
                Metadata);
        }
    }
}

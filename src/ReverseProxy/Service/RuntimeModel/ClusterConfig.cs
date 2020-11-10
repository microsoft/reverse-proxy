// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of a cluster
    /// that only change in reaction to configuration changes
    /// (e.g. health check options).
    /// </summary>
    /// <remarks>
    /// All members must remain immutable to avoid thread safety issues.
    /// Instead, instances of <see cref="ClusterConfig"/> are replaced
    /// in their entirety when values need to change.
    /// </remarks>
    public sealed class ClusterConfig
    {
        private readonly Cluster _cluster;

        public ClusterConfig(
            Cluster cluster,
            ClusterHealthCheckOptions healthCheckOptions,
            ClusterLoadBalancingOptions loadBalancingOptions,
            ClusterSessionAffinityOptions sessionAffinityOptions,
            HttpMessageInvoker httpClient,
            ClusterProxyHttpClientOptions httpClientOptions,
            ClusterProxyHttpRequestOptions httpRequestOptions,
            IReadOnlyDictionary<string, string> metadata)
        {
            _cluster = cluster;
            HealthCheckOptions = healthCheckOptions;
            LoadBalancingOptions = loadBalancingOptions;
            SessionAffinityOptions = sessionAffinityOptions;
            HttpClient = httpClient;
            HttpClientOptions = httpClientOptions;
            HttpRequestOptions = httpRequestOptions;
            Metadata = metadata;
        }

        public ClusterHealthCheckOptions HealthCheckOptions { get; }

        public ClusterLoadBalancingOptions LoadBalancingOptions { get; }

        public ClusterSessionAffinityOptions SessionAffinityOptions { get; }

        public ClusterProxyHttpClientOptions HttpClientOptions { get; }

        public ClusterProxyHttpRequestOptions HttpRequestOptions { get; }

        /// <summary>
        /// An <see cref="HttpMessageInvoker"/> that used for proxying requests to an upstream server.
        /// </summary>
        public HttpMessageInvoker HttpClient { get; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this cluster.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }

        internal bool HasConfigChanged(ClusterConfig newClusterConfig)
        {
            return !Cluster.Equals(_cluster, newClusterConfig._cluster);
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Service.Proxy;

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
        public ClusterConfig(
            Cluster cluster,
            ClusterHealthCheckOptions healthCheckOptions,
            string loadBalancingPolicy,
            ClusterSessionAffinityOptions sessionAffinityOptions,
            HttpMessageInvoker httpClient,
            ClusterProxyHttpClientOptions httpClientOptions,
            RequestProxyOptions httpRequestOptions,
            IReadOnlyDictionary<string, string> metadata)
        {
            Cluster = cluster;
            HealthCheckOptions = healthCheckOptions;
            LoadBalancingPolicy = loadBalancingPolicy;
            SessionAffinityOptions = sessionAffinityOptions;
            HttpClient = httpClient;
            HttpClientOptions = httpClientOptions;
            HttpRequestOptions = httpRequestOptions;
            Metadata = metadata;
        }

        internal Cluster Cluster { get; }

        public ClusterHealthCheckOptions HealthCheckOptions { get; }

        public string LoadBalancingPolicy { get; }

        public ClusterSessionAffinityOptions SessionAffinityOptions { get; }

        public ClusterProxyHttpClientOptions HttpClientOptions { get; }

        public RequestProxyOptions HttpRequestOptions { get; }

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
            return !Cluster.Equals(Cluster, newClusterConfig.Cluster);
        }
    }
}

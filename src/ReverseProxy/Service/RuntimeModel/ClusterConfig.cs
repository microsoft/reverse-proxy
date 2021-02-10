// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Service.LoadBalancing;

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
            HttpMessageInvoker httpClient,
            ILoadBalancingPolicy loadBalancingPolicy)
        {
            Options = cluster ?? throw new ArgumentNullException(nameof(cluster));
            HttpClient = httpClient;
            LoadBalancingPolicy = loadBalancingPolicy;
        }

        public Cluster Options { get; }

        /// <summary>
        /// An <see cref="HttpMessageInvoker"/> that used for proxying requests to an upstream server.
        /// </summary>
        public HttpMessageInvoker HttpClient { get; }

        /// <summary>
        /// An <see cref="ILoadBalancingPolicy"/> that used for picking the destination for the cluster.
        /// </summary>
        public ILoadBalancingPolicy LoadBalancingPolicy { get; }

        internal bool HasConfigChanged(ClusterConfig newClusterConfig)
        {
            return !Options.Equals(newClusterConfig.Options);
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Service.Proxy;

namespace Yarp.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of a route
    /// that only change in reaction to configuration changes.
    /// </summary>
    /// <remarks>
    /// All members must remain immutable to avoid thread safety issues.
    /// Instead, instances of <see cref="RouteConfig"/> are replaced
    /// in their entirety when values need to change.
    /// </remarks>
    public sealed class RouteConfig
    {
        /// <summary>
        /// Creates a new RouteConfig instance.
        /// </summary>
        public RouteConfig(
            ProxyRoute proxyRoute,
            ClusterInfo cluster,
            HttpTransformer transformer)
        {
            ProxyRoute = proxyRoute ?? throw new ArgumentNullException(nameof(proxyRoute));
            Cluster = cluster;
            Transformer = transformer;
        }

        // May not be populated if the cluster config is missing. https://github.com/microsoft/reverse-proxy/issues/797
        /// <summary>
        /// The ClusterInfo instance associated with this route.
        /// </summary>
        public ClusterInfo Cluster { get; }

        /// <summary>
        /// Transforms to apply for this route.
        /// </summary>
        public HttpTransformer Transformer { get; }

        /// <summary>
        /// The configuration data used to build this route.
        /// </summary>
        public ProxyRoute ProxyRoute { get; }

        internal bool HasConfigChanged(ProxyRoute newConfig, ClusterInfo cluster, int? routeRevision)
        {
            return Cluster != cluster || routeRevision != cluster?.Revision || !ProxyRoute.Equals(newConfig);
        }
    }
}

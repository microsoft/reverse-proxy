// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of a route
    /// that only change in reaction to configuration changes
    /// (e.g. rule, order, action, etc.).
    /// </summary>
    /// <remarks>
    /// All members must remain immutable to avoid thread safety issues.
    /// Instead, instances of <see cref="RouteConfig"/> are replaced
    /// in their entirety when values need to change.
    /// </remarks>
    internal sealed class RouteConfig
    {
        public RouteConfig(
            RouteInfo route,
            ProxyRoute proxyRoute,
            ClusterInfo cluster,
            HttpTransformer transformer)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            ProxyRoute = proxyRoute ?? throw new ArgumentNullException(nameof(proxyRoute));
            Order = proxyRoute.Order;
            Cluster = cluster;
            Transformer = transformer;
        }

        public RouteInfo Route { get; }

        public int? Order { get; }

        // May not be populated if the cluster config is missing.
        public ClusterInfo Cluster { get; }

        public HttpTransformer Transformer { get; }

        internal ProxyRoute ProxyRoute { get; }

        public bool HasConfigChanged(ProxyRoute newConfig, ClusterInfo cluster)
        {
            return Cluster != cluster || !ProxyRoute.Equals(newConfig);
        }
    }
}

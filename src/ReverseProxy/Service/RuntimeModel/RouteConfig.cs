// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.ReverseProxy.Abstractions;
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
        private readonly ProxyRoute _proxyRoute;

        public RouteConfig(
            RouteInfo route,
            ProxyRoute proxyRoute,
            ClusterInfo cluster,
            IReadOnlyList<AspNetCore.Http.Endpoint> aspNetCoreEndpoints,
            Transforms transforms)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            Endpoints = aspNetCoreEndpoints ?? throw new ArgumentNullException(nameof(aspNetCoreEndpoints));

            _proxyRoute = proxyRoute;
            Order = proxyRoute.Order;
            Cluster = cluster;
            Transforms = transforms;
            Metadata = proxyRoute.Metadata.ToImmutableDictionary();
        }

        public RouteInfo Route { get; }

        public int? Order { get; }

        // May not be populated if the cluster config is missing.
        public ClusterInfo Cluster { get; }

        public IReadOnlyList<AspNetCore.Http.Endpoint> Endpoints { get; }

        public Transforms Transforms { get; }

        public IReadOnlyDictionary<string, string> Metadata { get; }

        public bool HasConfigChanged(ProxyRoute newConfig, ClusterInfo cluster)
        {
            return Cluster != cluster
                || !ProxyRoute.Equals(_proxyRoute, newConfig);
        }
    }
}

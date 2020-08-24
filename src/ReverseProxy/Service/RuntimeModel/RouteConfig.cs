// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
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
        public RouteConfig(
            RouteInfo route,
            int configHash,
            int? order,
            ClusterInfo cluster,
            IReadOnlyList<AspNetCore.Http.Endpoint> aspNetCoreEndpoints,
            Transforms transforms)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            Endpoints = aspNetCoreEndpoints ?? throw new ArgumentNullException(nameof(aspNetCoreEndpoints));

            ConfigHash = configHash;
            Order = order;
            Cluster = cluster;
            Transforms = transforms;
        }

        public RouteInfo Route { get; }

        internal int ConfigHash { get; }

        public int? Order { get; }

        // May not be populated if the cluster config is missing.
        public ClusterInfo Cluster { get; }

        public IReadOnlyList<AspNetCore.Http.Endpoint> Endpoints { get; }

        public Transforms Transforms { get; }

        public bool HasConfigChanged(ProxyRoute newConfig, ClusterInfo cluster)
        {
            return Cluster != cluster
                || !ConfigHash.Equals(newConfig.GetConfigHash());
        }
    }
}

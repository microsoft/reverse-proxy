// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.ConfigModel;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of a route
    /// that only change in reaction to configuration changes
    /// (e.g. rule, priority, action, etc.).
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
            int? priority,
            BackendInfo backendOrNull,
            IReadOnlyList<AspNetCore.Http.Endpoint> aspNetCoreEndpoints,
            Transforms transforms)
        {
            Contracts.CheckValue(route, nameof(route));
            Contracts.CheckValue(aspNetCoreEndpoints, nameof(aspNetCoreEndpoints));

            Route = route;
            ConfigHash = configHash;
            Priority = priority;
            BackendOrNull = backendOrNull;
            Endpoints = aspNetCoreEndpoints;
            Transforms = transforms;
        }

        public RouteInfo Route { get; }

        internal int ConfigHash { get; }

        public int? Priority { get; }

        public BackendInfo BackendOrNull { get; }

        public IReadOnlyList<AspNetCore.Http.Endpoint> Endpoints { get; }

        public Transforms Transforms { get; }

        public bool HasConfigChanged(ParsedRoute newConfig, BackendInfo backendOrNull)
        {
            return BackendOrNull != backendOrNull
                || !ConfigHash.Equals(newConfig.GetConfigHash());
        }
    }
}

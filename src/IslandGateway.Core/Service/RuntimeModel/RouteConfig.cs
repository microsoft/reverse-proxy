// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using IslandGateway.Utilities;
using AspNetCore = Microsoft.AspNetCore;

namespace IslandGateway.Core.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of a route
    /// that only change in reaction to configuration changes
    /// (e.g. rule, priority, action, etc.).
    /// </summary>
    /// <remarks>
    /// All members must remain immutable to avoid thread safety issues.
    /// Instead, instances of <see cref="RouteConfig"/> are replaced
    /// in ther entirety when values need to change.
    /// </remarks>
    internal sealed class RouteConfig
    {
        public RouteConfig(
            RouteInfo route,
            string rule,
            int? priority,
            BackendInfo backendOrNull,
            IReadOnlyList<AspNetCore.Http.Endpoint> aspNetCoreEndpoints)
        {
            Contracts.CheckValue(route, nameof(route));
            Contracts.CheckValue(aspNetCoreEndpoints, nameof(aspNetCoreEndpoints));

            Route = route;
            Rule = rule;
            Priority = priority;
            BackendOrNull = backendOrNull;
            AspNetCoreEndpoints = aspNetCoreEndpoints;
        }

        public RouteInfo Route { get; }

        public string Rule { get; }

        public int? Priority { get; }

        public BackendInfo BackendOrNull { get; }

        public IReadOnlyList<AspNetCore.Http.Endpoint> AspNetCoreEndpoints { get; }
    }
}

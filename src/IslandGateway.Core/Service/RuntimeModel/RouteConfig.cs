// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using IslandGateway.Core.ConfigModel;
using IslandGateway.Core.Service;
using IslandGateway.Utilities;
using Microsoft.Extensions.FileSystemGlobbing;
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
            IList<RuleMatcherBase> matchers,
            int? priority,
            BackendInfo backendOrNull,
            IReadOnlyList<AspNetCore.Http.Endpoint> aspNetCoreEndpoints)
        {
            Contracts.CheckValue(route, nameof(route));
            Contracts.CheckValue(aspNetCoreEndpoints, nameof(aspNetCoreEndpoints));

            Route = route;
            Matchers = matchers;
            Priority = priority;
            BackendOrNull = backendOrNull;
            AspNetCoreEndpoints = aspNetCoreEndpoints;
        }

        public RouteInfo Route { get; }

        public IList<RuleMatcherBase> Matchers { get; }

        public int? Priority { get; }

        public BackendInfo BackendOrNull { get; }

        public IReadOnlyList<AspNetCore.Http.Endpoint> AspNetCoreEndpoints { get; }

        public bool HasConfigChanged(ParsedRoute newConfig, BackendInfo backendOrNull)
        {
            if (Matchers.Count != newConfig.Matchers.Count
                || Priority != newConfig.Priority
                || BackendOrNull != backendOrNull)
            {
                return true;
            }

            // The list is assumed to be stable, we can just walk through them in order.
            for (var i = 0; i < Matchers.Count; i++)
            {
                if (!Matchers[i].Equals(newConfig.Matchers[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using IslandGateway.Core.Abstractions;
using IslandGateway.Core.ConfigModel;
using IslandGateway.Utilities;

namespace IslandGateway.Core.Service
{
    internal class RouteParser : IRouteParser
    {
        public Result<ParsedRoute> ParseRoute(GatewayRoute route, IConfigErrorReporter errorReporter)
        {
            var results = new List<RuleMatcherBase>(3);

            if (route.Methods?.Length > 0)
            {
                results.Add(new MethodMatcher(route.Methods));
            }

            if (!string.IsNullOrEmpty(route.Host))
            {
                results.Add(new HostMatcher(route.Host));
            }

            if (!string.IsNullOrEmpty(route.Path))
            {
                results.Add(new PathMatcher(route.Path));
            }

            var parsedRoute = new ParsedRoute
            {
                RouteId = route.RouteId,
                Matchers = results,
                Priority = route.Priority,
                BackendId = route.BackendId,
                Metadata = route.Metadata,
            };

            return Result.Success(parsedRoute);
        }
    }
}

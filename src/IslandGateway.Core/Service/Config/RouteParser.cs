// <copyright file="RouteParser.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.Core.Abstractions;
using IslandGateway.Core.ConfigModel;
using IslandGateway.Utilities;

namespace IslandGateway.Core.Service
{
    internal class RouteParser : IRouteParser
    {
        private readonly IRuleParser _ruleParser;

        public RouteParser(IRuleParser ruleParser)
        {
            Contracts.CheckValue(ruleParser, nameof(ruleParser));
            _ruleParser = ruleParser;
        }

        public Result<ParsedRoute> ParseRoute(GatewayRoute route, IConfigErrorReporter errorReporter)
        {
            if (route.Rule == null)
            {
                errorReporter.ReportError(ConfigErrors.RouteBadRule, route.RouteId, $"Route '{route.RouteId}' did not specify a rule");
                return Result.Failure<ParsedRoute>();
            }

            var parsedRule = _ruleParser.Parse(route.Rule);
            if (!parsedRule.IsSuccess)
            {
                errorReporter.ReportError(ConfigErrors.RouteBadRule, route.RouteId, $"Route '{route.RouteId}' has an invalid rule: {parsedRule.Error} (rule: {route.Rule}");
                return Result.Failure<ParsedRoute>();
            }

            var parsedRoute = new ParsedRoute
            {
                RouteId = route.RouteId,
                Rule = route.Rule,
                Matchers = parsedRule.Value,
                Priority = route.Priority,
                BackendId = route.BackendId,
                Metadata = route.Metadata,
            };

            return Result.Success(parsedRoute);
        }
    }
}

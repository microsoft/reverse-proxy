// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace IslandGateway.Core.Service
{
    internal static class ConfigErrors
    {
        internal const string BackendDuplicateId = "Backend_DuplicateId";

        internal const string BackendEndpointDuplicateId = "BackendEndpoint_DuplicateId";
        internal const string BackendEndpointUnknownBackend = "BackendEndpoint_UnknownBackend";

        internal const string RouteDuplicateId = "Route_DuplicateId";
        internal const string RouteUnknownBackend = "Route_UnknownBackend";
        internal const string RouteNoBackends = "Route_NoBackends";
        internal const string RouteUnsupportedAction = "Route_UnsupportedAction";
        internal const string RouteBadRule = "Route_BadRule";

        internal const string ParsedRouteMissingId = "ParsedRoute_MissingId";
        internal const string ParsedRouteRuleHasNoMatchers = "ParsedRoute_RuleHasNoMatchers";
        internal const string ParsedRouteRuleMissingHostMatcher = "ParsedRoute_RuleMissingHostMatcher";
        internal const string ParsedRouteRuleMultipleHostMatchers = "ParsedRoute_RuleMultipleHostMatchers";
        internal const string ParsedRouteRuleMultiplePathMatchers = "ParsedRoute_RuleMultiplePathMatchers";
        internal const string ParsedRouteRuleInvalidMatcher = "ParsedRoute_RuleInvalidMatcher";
    }
}

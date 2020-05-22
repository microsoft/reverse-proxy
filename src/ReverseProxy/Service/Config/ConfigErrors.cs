// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service
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

        internal const string ParsedRouteMissingId = "ParsedRoute_MissingId";
        internal const string ParsedRouteRuleHasNoMatchers = "ParsedRoute_RuleHasNoMatchers";
        internal const string ParsedRouteRuleMissingHostMatcher = "ParsedRoute_RuleMissingHostMatcher";
        internal const string ParsedRouteRuleMultipleHostMatchers = "ParsedRoute_RuleMultipleHostMatchers";
        internal const string ParsedRouteRuleMultiplePathMatchers = "ParsedRoute_RuleMultiplePathMatchers";
        internal const string ParsedRouteRuleInvalidMatcher = "ParsedRoute_RuleInvalidMatcher";

        internal const string ConfigBuilderBackendIdMismatch = "ConfigBuilder_BackendIdMismatch";
        internal const string ConfigBuilderBackendSessionAffinityModeIsNull = "ConfigBuilder_BackendSessionAffinityModeIsNull";
        internal const string ConfigBuilderBackendNoProviderFoundForSessionAffinityMode = "ConfigBuilder_BackendNoProviderFoundForSessionAffinityMode";
        internal const string ConfigBuilderBackendMissingDestinationHandlerIsNull = "ConfigBuilder_MissingDestinationHandlerIsNull";
        internal const string ConfigBuilderBackendNoMissingDestinationHandlerFoundForSpecifiedName = "ConfigBuilder_NoMissingDestinationHandlerFoundForSpecifiedName";
        internal const string ConfigBuilderBackendException = "ConfigBuilder_BackendException";
        internal const string ConfigBuilderRouteException = "ConfigBuilder_RouteException";
    }
}

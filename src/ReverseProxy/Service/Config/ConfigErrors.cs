// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service
{
    internal static class ConfigErrors
    {
        internal const string ClusterDuplicateId = "Cluster_DuplicateId";

        internal const string DestinationDuplicateId = "Destination_DuplicateId";
        internal const string DestinationUnknownCluster = "Destination_UnknownCluster";

        internal const string RouteDuplicateId = "Route_DuplicateId";
        internal const string RouteUnknownCluster = "Route_UnknownCluster";
        internal const string RouteNoClusters = "Route_NoClusters";
        internal const string RouteUnsupportedAction = "Route_UnsupportedAction";

        internal const string ParsedRouteMissingId = "ParsedRoute_MissingId";
        internal const string ParsedRouteRuleHasNoMatchers = "ParsedRoute_RuleHasNoMatchers";
        internal const string ParsedRouteRuleInvalidMatcher = "ParsedRoute_RuleInvalidMatcher";
        internal const string ParsedRouteRuleInvalidAuthorizationPolicy = "ParsedRoute_RuleInvalidAuthorizationPolicy";
        internal const string TransformInvalid = "Transform_Invalid";

        internal const string ConfigBuilderClusterIdMismatch = "ConfigBuilder_ClusterIdMismatch";
        internal const string ConfigBuilderClusterNoProviderFoundForSessionAffinityMode = "ConfigBuilder_ClusterNoProviderFoundForSessionAffinityMode";
        internal const string ConfigBuilderClusterNoAffinityFailurePolicyFoundForSpecifiedName = "ConfigBuilder_NoAffinityFailurePolicyFoundForSpecifiedName";
        internal const string ConfigBuilderClusterException = "ConfigBuilder_ClusterException";
        internal const string ConfigBuilderRouteException = "ConfigBuilder_RouteException";
    }
}

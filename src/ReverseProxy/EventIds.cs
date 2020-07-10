// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.ReverseProxy
{
    internal static class EventIds
    {
        public static readonly EventId ApplyProxyConfig = new EventId(1, "ApplyProxyConfig");
        public static readonly EventId ApplyProxyConfigFailed = new EventId(2, "ApplyProxyConfigFailed");
        public static readonly EventId ConfigError = new EventId(3, "ConfigError");
        public static readonly EventId NoClusterFound = new EventId(4, "NoClusterFound");
        public static readonly EventId ClusterDataNotAvailable = new EventId(5, "ClusterDataNotAvailable");
        public static readonly EventId NoHealthyDestinations = new EventId(6, "NoHealthyDestinations");
        public static readonly EventId NoAvailableDestinations = new EventId(7, "NoAvailableDestinations");
        public static readonly EventId MultipleDestinationsAvailable = new EventId(8, "MultipleDestinationsAvailable");
        public static readonly EventId Proxying = new EventId(9, "Proxying");
        public static readonly EventId HealthCheckStopping = new EventId(10, "HealthCheckStopping");
        public static readonly EventId HealthCheckDisabled = new EventId(11, "HealthCheckDisabled");
        public static readonly EventId ProberCreated = new EventId(12, "ProberCreated");
        public static readonly EventId ProberUpdated = new EventId(13, "ProberUpdated");
        public static readonly EventId HealthCheckGracefulShutdown = new EventId(14, "HealthCheckGracefulShutdown");
        public static readonly EventId ProberStopped = new EventId(15, "ProberStopped");
        public static readonly EventId ProberFailed = new EventId(16, "ProberFailed");
        public static readonly EventId ProberChecked = new EventId(17, "ProberChecked");
        public static readonly EventId ProberGracefulShutdown = new EventId(18, "ProberGracefulShutdown");
        public static readonly EventId ProberStarted = new EventId(19, "ProberStarted");
        public static readonly EventId ProberResult = new EventId(20, "ProberResult");
        public static readonly EventId ClusterAdded = new EventId(21, "ClusterAdded");
        public static readonly EventId ClusterChanged = new EventId(22, "ClusterChanged");
        public static readonly EventId ClusterRemoved = new EventId(23, "ClusterRemoved");
        public static readonly EventId DestinationAdded = new EventId(24, "EndpointAdded");
        public static readonly EventId DestinationChanged = new EventId(25, "EndpointChanged");
        public static readonly EventId DestinationRemoved = new EventId(26, "EndpointRemoved");
        public static readonly EventId RouteAdded = new EventId(27, "RouteAdded");
        public static readonly EventId RouteChanged = new EventId(28, "RouteChanged");
        public static readonly EventId RouteRemoved = new EventId(29, "RouteRemoved");
        public static readonly EventId HttpDowngradeDetected = new EventId(30, "HttpDowngradeDetected");
        public static readonly EventId OperationStarted = new EventId(31, "OperationStarted");
        public static readonly EventId OperationEnded = new EventId(32, "OperationEnded");
        public static readonly EventId OperationFailed = new EventId(33, "OperationFailed");
        public static readonly EventId AffinityResolutionFailedForCluster = new EventId(34, "AffinityResolutionFailedForCluster");
        public static readonly EventId MultipleDestinationsOnClusterToEstablishRequestAffinity = new EventId(35, "MultipleDestinationsOnClusterToEstablishRequestAffinity");
        public static readonly EventId AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster = new EventId(36, "AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster");
        public static readonly EventId NoDestinationOnClusterToEstablishRequestAffinity = new EventId(37, "NoDestinationOnClusterToEstablishRequestAffinity");
        public static readonly EventId RequestAffinityKeyDecryptionFailed = new EventId(38, "RequestAffinityKeyDecryptionFailed");
        public static readonly EventId DestinationMatchingToAffinityKeyNotFound = new EventId(39, "DestinationMatchingToAffinityKeyNotFound");
        public static readonly EventId RequestAffinityHeaderHasMultipleValues = new EventId(40, "RequestAffinityHeaderHasMultipleValues");
        public static readonly EventId AffinityResolutionFailureWasHandledProcessingWillBeContinued = new EventId(41, "AffinityResolutionFailureWasHandledProcessingWillBeContinued");
    }
}

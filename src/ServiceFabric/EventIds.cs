// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.ServiceFabric
{
    internal static class EventIds
    {
        public static readonly EventId LoadData = new EventId(1, "ApplyProxyConfig");
        public static readonly EventId ErrorSignalingChange = new EventId(2, "ApplyProxyConfigFailed");
        public static readonly EventId DuplicateAppParameter = new EventId(3, "DuplicateAppParameter");
        public static readonly EventId GettingApplicationFailed = new EventId(4, "GettingApplicationFailed");
        public static readonly EventId GettingServiceFailed = new EventId(5, "GettingServiceFailed");
        public static readonly EventId InvalidServiceConfig = new EventId(6, "InvalidServiceConfig");
        public static readonly EventId ErrorLoadingServiceConfig = new EventId(7, "ErrorLoadingServiceConfig");
        public static readonly EventId ServiceDiscovered = new EventId(8, "ServiceDiscovered");
        public static readonly EventId GettingPartitionFailed = new EventId(9, "GettingPartitionFailed");
        public static readonly EventId GettingReplicaFailed = new EventId(10, "GettingReplicaFailed");
        public static readonly EventId UnhealthyReplicaSkipped = new EventId(11, "UnhealthyReplicaSkipped");
        public static readonly EventId IneligibleEndpointSkipped = new EventId(12, "IneligibleEndpointSkipped");
        public static readonly EventId InvalidReplicaConfig = new EventId(13, "InvalidReplicaConfig");
        public static readonly EventId ErrorLoadingReplicaConfig = new EventId(14, "ErrorLoadingReplicaConfig");
        public static readonly EventId InvalidReplicaSelectionMode = new EventId(15, "InvalidReplicaSelectionMode");
        public static readonly EventId ServiceHealthReportFailed = new EventId(16, "ServiceHealthReportFailed");
        public static readonly EventId ReplicaHealthReportFailedInvalidServiceKind = new EventId(17, "ReplicaHealthReportFailedInvalidServiceKind");
        public static readonly EventId ReplicaHealthReportFailed = new EventId(18, "ReplicaHealthReportFailed");
        public static readonly EventId InvalidApplicationParameter = new EventId(19, "InvalidApplicationParameter");
        public static readonly EventId StartWithoutInitialServiceFabricDiscovery = new EventId(20, "StartWithoutInitialServiceFabricDiscovery");
        public static readonly EventId WaitingForInitialServiceFabricDiscovery = new EventId(21, "WaitingForInitialServiceFabricDiscovery");
        public static readonly EventId StartingServiceFabricDiscoveryLoop = new EventId(22, "StartingServiceFabricDiscoveryLoop");
        public static readonly EventId ServiceFabricDiscoveryLoopEndedGracefully = new EventId(23, "ServiceFabricDiscoveryLoopEndedGracefully");
        public static readonly EventId ServiceFabricDiscoveryLoopFailed = new EventId(24, "ServiceFabricDiscoveryLoopFailed");
        public static readonly EventId StartCacheOperation = new EventId(25, "StartCacheOperation");
        public static readonly EventId StartInnerCacheOperation = new EventId(26, "StartInnerCacheOperation");
        public static readonly EventId CacheOperationCompleted = new EventId(27, "CacheOperationCompleted");
    }
}

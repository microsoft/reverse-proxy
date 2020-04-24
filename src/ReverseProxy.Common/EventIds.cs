// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.ReverseProxy.Common
{
    public static class EventIds
    {
        public static readonly EventId ApplyProxyConfig = new EventId(1, "ApplyProxyConfig");
        public static readonly EventId ApplyProxyConfigFailed = new EventId(2, "ApplyProxyConfigFailed");
        public static readonly EventId ConfigError = new EventId(3, "ConfigError");
        public static readonly EventId NoBackendFound = new EventId(4, "NoBackendFound");
        public static readonly EventId BackendDataNotAvailable = new EventId(5, "BackendDataNotAvailable");
        public static readonly EventId NoHealthyEndpoints = new EventId(6, "NoHealthyEndpoints");
        public static readonly EventId NoAvailableEndpoints = new EventId(7, "NoAvailableEndpoints");
        public static readonly EventId MultipleEndpointsAvailable = new EventId(8, "MultipleEndpointsAvailable");
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
        public static readonly EventId BackendAdded = new EventId(21, "BackendAdded");
        public static readonly EventId BackendChanged = new EventId(22, "BackendChanged");
        public static readonly EventId BackendRemoved = new EventId(23, "BackendRemoved");
        public static readonly EventId EndpointAdded = new EventId(24, "EndpointAdded");
        public static readonly EventId EndpointChanged = new EventId(25, "EndpointChanged");
        public static readonly EventId EndpointRemoved = new EventId(26, "EndpointRemoved");
        public static readonly EventId RouteAdded = new EventId(27, "RouteAdded");
        public static readonly EventId RouteChanged = new EventId(28, "RouteChanged");
        public static readonly EventId RouteRemoved = new EventId(29, "RouteRemoved");
        public static readonly EventId HttpDowngradeDeteced = new EventId(30, "HttpDowngradeDeteced");
        public static readonly EventId OperationStarted = new EventId(31, "OperationStarted");
        public static readonly EventId OperationEnded = new EventId(32, "OperationEnded");
        public static readonly EventId OperationFailed = new EventId(33, "OperationFailed");
    }
}

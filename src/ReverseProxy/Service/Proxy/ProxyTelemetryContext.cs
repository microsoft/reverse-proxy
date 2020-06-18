// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.Proxy
{
    // Note: This only makes sense as a struct if it remains small
    internal readonly struct ProxyTelemetryContext
    {
        public ProxyTelemetryContext(
            string clusterId,
            string routeId,
            string destinationId)
        {
            ClusterId = clusterId;
            RouteId = routeId;
            DestinationId = destinationId;
        }

        public string ClusterId { get; }
        public string RouteId { get; }
        public string DestinationId { get; }
    }
}

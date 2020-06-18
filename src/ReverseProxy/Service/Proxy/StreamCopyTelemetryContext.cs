// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.Proxy
{
    // Note: This only makes sense as a struct if it remains small
    internal readonly struct StreamCopyTelemetryContext
    {
        public StreamCopyTelemetryContext(
            string direction,
            string clusterId,
            string routeId,
            string destinationId)
        {
            Direction = direction;
            ClusterId = clusterId;
            RouteId = routeId;
            DestinationId = destinationId;
        }

        public string Direction { get; }
        public string ClusterId { get; }
        public string RouteId { get; }
        public string DestinationId { get; }
    }
}

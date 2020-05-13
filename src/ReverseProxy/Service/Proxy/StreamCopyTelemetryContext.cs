// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.Proxy
{
    // Note: This only makes sense as a struct if it remains small
    internal readonly struct StreamCopyTelemetryContext
    {
        public StreamCopyTelemetryContext(
            string direction,
            string backendId,
            string routeId,
            string destinationId)
        {
            Direction = direction;
            BackendId = backendId;
            RouteId = routeId;
            DestinationId = destinationId;
        }

        public string Direction { get; }
        public string BackendId { get; }
        public string RouteId { get; }
        public string DestinationId { get; }
    }
}

// <copyright file="StreamCopyTelemetryContext.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace IslandGateway.Core.Service.Proxy
{
    // Note: This only makes sense as a struct if it remains small
    internal readonly struct StreamCopyTelemetryContext
    {
        public StreamCopyTelemetryContext(
            string direction,
            string backendId,
            string routeId,
            string endpointId)
        {
            this.Direction = direction;
            this.BackendId = backendId;
            this.RouteId = routeId;
            this.EndpointId = endpointId;
        }

        public string Direction { get; }
        public string BackendId { get; }
        public string RouteId { get; }
        public string EndpointId { get; }
    }
}

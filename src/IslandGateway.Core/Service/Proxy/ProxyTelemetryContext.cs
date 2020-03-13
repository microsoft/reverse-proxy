// <copyright file="ProxyTelemetryContext.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace IslandGateway.Core.Service.Proxy
{
    // Note: This only makes sense as a struct if it remains small
    internal readonly struct ProxyTelemetryContext
    {
        public ProxyTelemetryContext(
            string backendId,
            string routeId,
            string endpointId)
        {
            BackendId = backendId;
            RouteId = routeId;
            EndpointId = endpointId;
        }

        public string BackendId { get; }
        public string RouteId { get; }
        public string EndpointId { get; }
    }
}

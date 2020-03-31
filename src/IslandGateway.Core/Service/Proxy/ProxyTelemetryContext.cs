// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Core.Service.Proxy
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

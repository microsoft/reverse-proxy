// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Service.Proxy;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public interface IProxyTelemetryConsumer
    {
        void OnProxyStart(DateTime timestamp, string destinationPrefix);

        void OnProxyStop(DateTime timestamp, int statusCode);

        void OnProxyFailed(DateTime timestamp, ProxyError error);

        void OnProxyStage(DateTime timestamp, ProxyStage stage);

        void OnContentTransferring(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime);

        void OnContentTransferred(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime, TimeSpan firstReadTime);

        void OnProxyInvoke(DateTime timestamp, string clusterId, string routeId, string destinationId);
    }
}

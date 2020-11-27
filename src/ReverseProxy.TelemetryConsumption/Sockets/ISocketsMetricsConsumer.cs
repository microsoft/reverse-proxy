// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public interface ISocketsMetricsConsumer
    {
        void OnSocketsMetrics(SocketsMetrics oldMetrics, SocketsMetrics newMetrics);
    }
}

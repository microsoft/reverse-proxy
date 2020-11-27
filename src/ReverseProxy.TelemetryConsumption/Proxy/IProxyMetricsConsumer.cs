// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public interface IProxyMetricsConsumer
    {
        void OnProxyMetrics(ProxyMetrics oldMetrics, ProxyMetrics newMetrics);
    }
}

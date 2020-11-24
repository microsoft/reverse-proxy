// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public interface IHttpMetricsConsumer
    {
        void OnHttpMetrics(HttpMetrics oldMetrics, HttpMetrics newMetrics);
    }
}

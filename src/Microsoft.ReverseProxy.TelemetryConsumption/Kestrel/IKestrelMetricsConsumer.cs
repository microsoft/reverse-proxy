// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public interface IKestrelMetricsConsumer
    {
        void OnKestrelMetrics(KestrelMetrics oldMetrics, KestrelMetrics newMetrics);
    }
}

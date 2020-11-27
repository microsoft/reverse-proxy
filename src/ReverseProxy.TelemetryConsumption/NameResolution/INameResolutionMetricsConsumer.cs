// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public interface INameResolutionMetricsConsumer
    {
        void OnNameResolutionMetrics(NameResolutionMetrics oldMetrics, NameResolutionMetrics newMetrics);
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public interface INetSecurityMetricsConsumer
    {
        void OnNetSecurityMetrics(NetSecurityMetrics oldMetrics, NetSecurityMetrics newMetrics);
    }
}

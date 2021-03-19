// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    /// <summary>
    /// A consumer of <see cref="ProxyMetrics"/>.
    /// </summary>
    public interface IProxyMetricsConsumer
    {
        /// <summary>
        /// Processes <see cref="ProxyMetrics"/> from the last event counter interval.
        /// </summary>
        /// <param name="oldMetrics">Metrics collected in the previous interval.</param>
        /// <param name="newMetrics">Metrics collected in the last interval.</param>
        void OnProxyMetrics(ProxyMetrics oldMetrics, ProxyMetrics newMetrics);
    }
}

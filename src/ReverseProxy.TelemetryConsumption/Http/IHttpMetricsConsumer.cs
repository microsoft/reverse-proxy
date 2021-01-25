// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    /// <summary>
    /// A consumer of <see cref="HttpMetrics"/>.
    /// </summary>
    public interface IHttpMetricsConsumer
    {
        /// <summary>
        /// Processes <see cref="HttpMetrics"/> from the last event counter interval.
        /// </summary>
        /// <param name="oldMetrics">Metrics collected in the previous interval.</param>
        /// <param name="newMetrics">Metrics collected in the last interval.</param>
        void OnHttpMetrics(HttpMetrics oldMetrics, HttpMetrics newMetrics);
    }
}

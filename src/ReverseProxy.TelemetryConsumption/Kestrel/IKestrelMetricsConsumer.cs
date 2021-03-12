// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    /// <summary>
    /// A consumer of <see cref="KestrelMetrics"/>.
    /// </summary>
    public interface IKestrelMetricsConsumer
    {
        /// <summary>
        /// Processes <see cref="KestrelMetrics"/> from the last event counter interval.
        /// </summary>
        /// <param name="oldMetrics">Metrics collected in the previous interval.</param>
        /// <param name="newMetrics">Metrics collected in the last interval.</param>
        void OnKestrelMetrics(KestrelMetrics oldMetrics, KestrelMetrics newMetrics);
    }
}

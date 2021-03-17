// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    /// <summary>
    /// A consumer of <see cref="SocketsMetrics"/>.
    /// </summary>
    public interface ISocketsMetricsConsumer
    {
        /// <summary>
        /// Processes <see cref="SocketsMetrics"/> from the last event counter interval.
        /// </summary>
        /// <param name="oldMetrics">Metrics collected in the previous interval.</param>
        /// <param name="newMetrics">Metrics collected in the last interval.</param>
        void OnSocketsMetrics(SocketsMetrics oldMetrics, SocketsMetrics newMetrics);
    }
}

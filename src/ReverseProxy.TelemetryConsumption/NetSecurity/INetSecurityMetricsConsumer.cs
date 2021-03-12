// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    /// <summary>
    /// A consumer of <see cref="NetSecurityMetrics"/>.
    /// </summary>
    public interface INetSecurityMetricsConsumer
    {
        /// <summary>
        /// Processes <see cref="NetSecurityMetrics"/> from the last event counter interval.
        /// </summary>
        /// <param name="oldMetrics">Metrics collected in the previous interval.</param>
        /// <param name="newMetrics">Metrics collected in the last interval.</param>
        void OnNetSecurityMetrics(NetSecurityMetrics oldMetrics, NetSecurityMetrics newMetrics);
    }
}

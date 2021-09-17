// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.Telemetry.Consumption
{
    /// <summary>
    /// A consumer of <see cref="ForwarderMetrics"/>.
    /// </summary>
    public interface IForwarderMetricsConsumer
    {
        /// <summary>
        /// Processes <see cref="ForwarderMetrics"/> from the last event counter interval.
        /// </summary>
        /// <param name="oldMetrics">Metrics collected in the previous interval.</param>
        /// <param name="newMetrics">Metrics collected in the last interval.</param>
        void OnForwarderMetrics(ForwarderMetrics oldMetrics, ForwarderMetrics newMetrics);
    }
}

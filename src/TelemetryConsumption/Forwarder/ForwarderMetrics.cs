// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.Telemetry.Consumption
{
    /// <summary>
    /// Represents metrics reported by the Yarp.ReverseProxy event counters.
    /// </summary>
    public sealed class ForwarderMetrics
    {
        /// <summary>
        /// Timestamp of when this <see cref="ForwarderMetrics"/> instance was created.
        /// </summary>
        public DateTime Timestamp { get; internal set; }

        /// <summary>
        /// Number of proxy requests started since telemetry was enabled.
        /// </summary>
        public long RequestsStarted { get; internal set; }

        /// <summary>
        /// Number of proxy requests started in the last metrics interval.
        /// </summary>
        public long RequestsStartedRate { get; internal set; }

        /// <summary>
        /// Number of proxy requests that failed since telemetry was enabled.
        /// </summary>
        public long RequestsFailed { get; internal set; }

        /// <summary>
        /// Number of active proxy requests that have started but not yet completed or failed.
        /// </summary>
        public long CurrentRequests { get; internal set; }

        internal ForwarderMetrics() { }
    }
}

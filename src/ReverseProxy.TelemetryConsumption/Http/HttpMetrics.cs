// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public sealed class HttpMetrics
    {
        public DateTime Timestamp { get; internal set; }
        public long RequestsStarted { get; internal set; }
        public long RequestsStartedRate { get; internal set; }
        public long RequestsFailed { get; internal set; }
        public long RequestsFailedRate { get; internal set; }
        public long CurrentRequests { get; internal set; }
        public long CurrentHttp11Connections { get; internal set; }
        public long CurrentHttp20Connections { get; internal set; }
        public TimeSpan Http11RequestsQueueDuration { get; internal set; }
        public TimeSpan Http20RequestsQueueDuration { get; internal set; }

        internal HttpMetrics() { }
    }
}

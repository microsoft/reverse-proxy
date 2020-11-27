// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public sealed class ProxyMetrics
    {
        public DateTime Timestamp { get; internal set; }
        public long RequestsStarted { get; internal set; }
        public long RequestsStartedRate { get; internal set; }
        public long RequestsFailed { get; internal set; }
        public long CurrentRequests { get; internal set; }

        internal ProxyMetrics() { }
    }
}

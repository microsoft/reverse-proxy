// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public sealed class KestrelMetrics
    {
        public DateTime Timestamp { get; internal set; }
        public long ConnectionRate { get; internal set; }
        public long TotalConnections { get; internal set; }
        public long TlsHandshakeRate { get; internal set; }
        public long TotalTlsHandshakes { get; internal set; }
        public long CurrentTlsHandshakes { get; internal set; }
        public long FailedTlsHandshakes { get; internal set; }
        public long CurrentConnections { get; internal set; }
        public long ConnectionQueueLength { get; internal set; }
        public long RequestQueueLength { get; internal set; }
        public long CurrentUpgradedRequests { get; internal set; }

        internal KestrelMetrics() { }
    }
}

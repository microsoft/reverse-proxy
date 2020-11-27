// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public sealed class NetSecurityMetrics
    {
        public DateTime Timestamp { get; internal set; }
        public long TlsHandshakeRate { get; internal set; }
        public long TotalTlsHandshakes { get; internal set; }
        public long CurrentTlsHandshakes { get; internal set; }
        public long FailedTlsHandshakes { get; internal set; }
        public long TlsSessionsOpen { get; internal set; }
        public long Tls10SessionsOpen { get; internal set; }
        public long Tls11SessionsOpen { get; internal set; }
        public long Tls12SessionsOpen { get; internal set; }
        public long Tls13SessionsOpen { get; internal set; }
        public TimeSpan TlsHandshakeDuration { get; internal set; }
        public TimeSpan Tls10HandshakeDuration { get; internal set; }
        public TimeSpan Tls11HandshakeDuration { get; internal set; }
        public TimeSpan Tls12HandshakeDuration { get; internal set; }
        public TimeSpan Tls13HandshakeDuration { get; internal set; }

        internal NetSecurityMetrics() { }
    }
}

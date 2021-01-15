// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    /// <summary>
    /// Represents metrics reported by the System.Net.Security event counters.
    /// </summary>
    public sealed class NetSecurityMetrics
    {
        /// <summary>
        /// Timestamp of when this <see cref="NetSecurityMetrics"/> instance was created.
        /// </summary>
        public DateTime Timestamp { get; internal set; }

        /// <summary>
        /// Number of TLS handshakes completed in the last metrics interval.
        /// </summary>
        public long TlsHandshakeRate { get; internal set; }

        /// <summary>
        /// Number of TLS handshakes completed since telemetry was enabled.
        /// </summary>
        public long TotalTlsHandshakes { get; internal set; }

        /// <summary>
        /// Number of active TLS handshakes that have started but not yet completed or failed.
        /// </summary>
        public long CurrentTlsHandshakes { get; internal set; }

        /// <summary>
        /// Number of TLS handshakes that failed since telemetry was enabled.
        /// </summary>
        public long FailedTlsHandshakes { get; internal set; }

        /// <summary>
        /// Number of currently open TLS sessions.
        /// </summary>
        public long TlsSessionsOpen { get; internal set; }

        /// <summary>
        /// Number of currently open TLS 1.0 sessions.
        /// </summary>
        public long Tls10SessionsOpen { get; internal set; }

        /// <summary>
        /// Number of currently open TLS 1.1 sessions.
        /// </summary>
        public long Tls11SessionsOpen { get; internal set; }

        /// <summary>
        /// Number of currently open TLS 1.2 sessions.
        /// </summary>
        public long Tls12SessionsOpen { get; internal set; }

        /// <summary>
        /// Number of currently open TLS 1.3 sessions.
        /// </summary>
        public long Tls13SessionsOpen { get; internal set; }

        /// <summary>
        /// Average duration of all TLS handshakes completed in the last metrics interval.
        /// </summary>
        public TimeSpan TlsHandshakeDuration { get; internal set; }

        /// <summary>
        /// Average duration of all TLS 1.0 handshakes completed in the last metrics interval.
        /// </summary>
        public TimeSpan Tls10HandshakeDuration { get; internal set; }

        /// <summary>
        /// Average duration of all TLS 1.1 handshakes completed in the last metrics interval.
        /// </summary>
        public TimeSpan Tls11HandshakeDuration { get; internal set; }

        /// <summary>
        /// Average duration of all TLS 1.2 handshakes completed in the last metrics interval.
        /// </summary>
        public TimeSpan Tls12HandshakeDuration { get; internal set; }

        /// <summary>
        /// Average duration of all TLS 1.3 handshakes completed in the last metrics interval.
        /// </summary>
        public TimeSpan Tls13HandshakeDuration { get; internal set; }

        internal NetSecurityMetrics() { }
    }
}

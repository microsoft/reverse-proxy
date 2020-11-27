// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public sealed class SocketsMetrics
    {
        public DateTime Timestamp { get; internal set; }
        public long OutgoingConnectionsEstablished { get; internal set; }
        public long IncomingConnectionsEstablished { get; internal set; }
        public long BytesReceived { get; internal set; }
        public long BytesSent { get; internal set; }
        public long DatagramsReceived { get; internal set; }
        public long DatagramsSent { get; internal set; }

        internal SocketsMetrics() { }
    }
}

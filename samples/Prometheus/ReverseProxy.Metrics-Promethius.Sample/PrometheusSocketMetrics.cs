// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET5_0_OR_GREATER

using Yarp.ReverseProxy.Telemetry.Consumption;
using Prometheus;

namespace Yarp.Sample
{

    public sealed class PrometheusSocketMetrics : ISocketsMetricsConsumer
    {
        private static readonly Counter _outgoingConnectionsEstablished = Metrics.CreateCounter(
            "yarp_sockets_outgoing_connections_established",
            "Number of outgoing (Connect) Socket connections established"
            );


        private static readonly Counter _incomingConnectionsEstablished = Metrics.CreateCounter(
            "yarp_sockets_incomming_connections_established",
            "Number of incoming (Accept) Socket connections established"
            );

        private static readonly Counter _bytesReceived = Metrics.CreateCounter(
            "yarp_sockets_bytes_recieved",
            "Number of bytes received"
            );

        private static readonly Counter _bytesSent = Metrics.CreateCounter(
            "yarp_sockets_bytes_sent",
            "Number of bytes sent"
            );

        private static readonly Counter _datagramsReceived = Metrics.CreateCounter(
            "yarp_sockets_datagrams_received",
            "Number of datagrams received"
            );

        private static readonly Counter _datagramsSent = Metrics.CreateCounter(
            "yarp_sockets_datagrams_sent",
            "Number of datagrams Sent"
            );

        public void OnSocketsMetrics(SocketsMetrics oldMetrics, SocketsMetrics newMetrics)
        {
            _outgoingConnectionsEstablished.IncTo(newMetrics.OutgoingConnectionsEstablished);
            _incomingConnectionsEstablished.IncTo(newMetrics.IncomingConnectionsEstablished);
            _bytesReceived.IncTo(newMetrics.BytesReceived);
            _bytesSent.IncTo(newMetrics.BytesSent);
            _datagramsReceived.IncTo(newMetrics.DatagramsReceived);
            _datagramsSent.IncTo(newMetrics.DatagramsSent);
        }
    }
}
#endif

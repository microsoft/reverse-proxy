
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET5_0_OR_GREATER

using System;
using Yarp.ReverseProxy.Telemetry.Consumption;
using Prometheus;

namespace Yarp.Sample
{
    public sealed class PrometheusKestrelMetrics : IKestrelMetricsConsumer
    {
        private static readonly Counter _totalConnections = Metrics.CreateCounter(
            "yarp_kestrel_total_connections",
            "Number of incomming connections opened"
            );

        private static readonly Counter _totalTlsHandshakes = Metrics.CreateCounter(
            "yarp_kestrel_total_tls_Handshakes",
            "Numer of TLS handshakes started"
            );

        private static readonly Gauge _currentTlsHandshakes = Metrics.CreateGauge(
            "yarp_kestrel_current_tls_handshakes",
            "Number of active TLS handshakes that have started but not yet completed or failed"
            );

        private static readonly Counter _failedTlsHandshakes = Metrics.CreateCounter(
            "yarp_kestrel_failed_tls_handshakes",
            "Number of TLS handshakes that failed"
            );

        private static readonly Gauge _currentConnections = Metrics.CreateGauge(
            "yarp_kestrel_current_connections",
            "Number of currently open incomming connections"
            );

        private static readonly Gauge _connectionQueueLength = Metrics.CreateGauge(
            "yarp_kestrel_connection_queue_length",
            "Number of connections on the queue."
            );

        private static readonly Gauge _requestQueueLength = Metrics.CreateGauge(
            "yarp_kestrel_request_queue_length",
            "Number of requests on the queue"
            );

        public void OnKestrelMetrics(KestrelMetrics oldMetrics, KestrelMetrics newMetrics)
        {
            _totalConnections.IncTo(newMetrics.TotalConnections);
            _totalTlsHandshakes.IncTo(newMetrics.TotalTlsHandshakes);
            _currentTlsHandshakes.Set(newMetrics.CurrentTlsHandshakes);
            _failedTlsHandshakes.IncTo(newMetrics.FailedTlsHandshakes);
            _currentConnections.Set(newMetrics.CurrentConnections);
            _connectionQueueLength.Set(newMetrics.ConnectionQueueLength);
            _requestQueueLength.Set(newMetrics.RequestQueueLength);
        }
    }
}
#endif

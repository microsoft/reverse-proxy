
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET5_0_OR_GREATER
using System;
using Yarp.ReverseProxy.Telemetry.Consumption;
using Prometheus;

namespace Yarp.Sample
{
    /// <summary>
    /// Collects outbound http metrics and exposes them using prometheus-net
    /// </summary>
    public sealed class PrometheusOutboundHttpMetrics: IHttpMetricsConsumer
    {
        private static readonly double CUBE_ROOT_10 = Math.Pow(10, (1.0 / 3));

        private static readonly Counter _outboundRequestsStarted = Metrics.CreateCounter(
            "yarp_outbound_http_requests_started",
            "Number of outbound requests inititated by the proxy"
            );

        private static readonly Counter _outboundRequestsFailed = Metrics.CreateCounter(
            "yarp_outbound_http_requests_failed",
            "Number of outbound requests failed"
            );

        private static readonly Gauge _outboundCurrentRequests =Metrics.CreateGauge(
            "yarp_outbound_http_current_requests",
            "Number of active outbound requests that have started but not yet completed or failed"
            );

        private static readonly Gauge _outboundCurrentHttp11Connections = Metrics.CreateGauge(
            "yarp_outbound_http11_connections",
            "Number of currently open HTTP 1.1 connections"
            );

        private static readonly Gauge _outboundCurrentHttp20Connections= Metrics.CreateGauge(
            "yarp_outbound_http20_connections",
            "Number of active proxy requests that have started but not yet completed or failed"
            );

        private static readonly Histogram _outboundHttp11RequestQueueDuration= Metrics.CreateHistogram(
            "yarp_outbound_http11_request_queue_duration (ms)",
            "Average time spent on queue for HTTP 1.1 requests that hit the MaxConnectionsPerServer limit in the last metrics interval",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(10, CUBE_ROOT_10, 10)
            });

        private static readonly Histogram _outboundHttp20RequestQueueDuration = Metrics.CreateHistogram(
            "yarp_outbound_http20_request_queue_duration (ms)",
            "Average time spent on queue for HTTP 2.0 requests that hit the MAX_CONCURRENT_STREAMS limit on the connection in the last metrics interval",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(10, CUBE_ROOT_10, 10)
            });

        public void OnHttpMetrics(HttpMetrics oldMetrics, HttpMetrics newMetrics)
        {
            _outboundRequestsStarted.IncTo(newMetrics.RequestsStarted);
            _outboundRequestsFailed.IncTo(newMetrics.RequestsFailed);
            _outboundCurrentRequests.Set(newMetrics.CurrentRequests);
            _outboundCurrentHttp11Connections.Set(newMetrics.CurrentHttp11Connections);
            _outboundCurrentHttp20Connections.Set(newMetrics.CurrentHttp20Connections);
            _outboundHttp11RequestQueueDuration.Observe(newMetrics.Http11RequestsQueueDuration.TotalMilliseconds);
            _outboundHttp20RequestQueueDuration.Observe(newMetrics.Http20RequestsQueueDuration.TotalMilliseconds);
        }
    }
}
#endif

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.Telemetry.Consumption;
using Prometheus;

namespace Yarp.Sample
{
    public sealed class PrometheusForwarderMetrics : IForwarderMetricsConsumer
    {
        private static readonly Counter _requestsStarted = Metrics.CreateCounter(
            "yarp_proxy_requests_started",
            "Number of requests inititated through the proxy"
            );

        private static readonly Counter _requestsFailed = Metrics.CreateCounter(
            "yarp_proxy_requests_failed",
            "Number of proxy requests that failed"
            );

        private static readonly Gauge _CurrentRequests = Metrics.CreateGauge(
            "yarp_proxy_current_requests",
            "Number of active proxy requests that have started but not yet completed or failed"
            );

        public void OnForwarderMetrics(ForwarderMetrics oldMetrics, ForwarderMetrics newMetrics)
        {
            _requestsStarted.IncTo(newMetrics.RequestsStarted);
            _requestsFailed.IncTo(newMetrics.RequestsFailed);
            _CurrentRequests.Set(newMetrics.CurrentRequests);
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Yarp.ReverseProxy.Telemetry.Consumption;
using Prometheus;

namespace Yarp.Sample
{
    public sealed class ProxyMetricsConsumer : IProxyMetricsConsumer
    {
        private static readonly Counter _allRequestsStarted = Metrics.CreateCounter(
            "yarp_all_requests_started",
            "Number of requests inititated through the proxy"
            );

        public void OnProxyMetrics(ProxyMetrics oldMetrics, ProxyMetrics newMetrics)
        {
            _allRequestsStarted.IncTo(newMetrics.RequestsStarted);

            //var elapsed = newMetrics.Timestamp - oldMetrics.Timestamp;
            //var newRequests = newMetrics.RequestsStarted - oldMetrics.RequestsStarted;
            //Console.Title = $"Proxied {newMetrics.RequestsStarted} requests ({newRequests} in the last {(int)elapsed.TotalMilliseconds} ms)";
        }
    }
}

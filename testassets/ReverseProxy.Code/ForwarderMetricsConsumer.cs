// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Yarp.ReverseProxy.Telemetry.Consumption;

namespace Yarp.ReverseProxy.Sample
{
    public sealed class ProxyMetricsConsumer : IForwarderMetricsConsumer
    {
        public void OnForwarderMetrics(ForwarderMetrics oldMetrics, ForwarderMetrics newMetrics)
        {
            var elapsed = newMetrics.Timestamp - oldMetrics.Timestamp;
            var newRequests = newMetrics.RequestsStarted - oldMetrics.RequestsStarted;
            Console.Title = $"Proxied {newMetrics.RequestsStarted} requests ({newRequests} in the last {(int)elapsed.TotalMilliseconds} ms)";
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Yarp.Telemetry.Consumption;

namespace Yarp.ReverseProxy.Sample
{
    public sealed class ForwarderMetricsConsumer : IMetricsConsumer<ForwarderMetrics>
    {
        public void OnMetrics(ForwarderMetrics previous, ForwarderMetrics current)
        {
            var elapsed = current.Timestamp - previous.Timestamp;
            var newRequests = current.RequestsStarted - previous.RequestsStarted;
            Console.Title = $"Proxied {current.RequestsStarted} requests ({newRequests} in the last {(int)elapsed.TotalMilliseconds} ms)";
        }
    }
}

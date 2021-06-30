// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET

using Yarp.ReverseProxy.Telemetry.Consumption;
using Prometheus;

namespace Yarp.Sample
{
    public sealed class PrometheusDnsMetrics : INameResolutionMetricsConsumer
    {
        private static readonly Counter _dnsLookupsRequested = Metrics.CreateCounter(
            "yarp_dns_lookups_requested",
            "Number of DNS lookups requested"
            );

        private static readonly Gauge _averageLookupDuration = Metrics.CreateGauge(
            "yarp_dns_average_lookup_duration",
            "Average DNS lookup duration"
            );

        public void OnNameResolutionMetrics(NameResolutionMetrics oldMetrics, NameResolutionMetrics newMetrics)
        {
            _dnsLookupsRequested.IncTo(newMetrics.DnsLookupsRequested);
            _averageLookupDuration.Set(newMetrics.AverageLookupDuration.TotalMilliseconds);
        }
    }
}
#endif

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET

using Yarp.Telemetry.Consumption;
using Prometheus;

namespace Yarp.Sample
{
    public sealed class PrometheusDnsMetrics : IMetricsConsumer<NameResolutionMetrics>
    {
        private static readonly Counter _dnsLookupsRequested = Metrics.CreateCounter(
            "yarp_dns_lookups_requested",
            "Number of DNS lookups requested"
            );

        private static readonly Gauge _averageLookupDuration = Metrics.CreateGauge(
            "yarp_dns_average_lookup_duration",
            "Average DNS lookup duration"
            );

        public void OnMetrics(NameResolutionMetrics previous, NameResolutionMetrics current)
        {
            _dnsLookupsRequested.IncTo(current.DnsLookupsRequested);
            _averageLookupDuration.Set(current.AverageLookupDuration.TotalMilliseconds);
        }
    }
}
#endif

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public sealed class NameResolutionMetrics
    {
        public DateTime Timestamp { get; internal set; }
        public long DnsLookupsRequested { get; internal set; }
        public TimeSpan AverageLookupDuration { get; internal set; }

        internal NameResolutionMetrics() { }
    }
}

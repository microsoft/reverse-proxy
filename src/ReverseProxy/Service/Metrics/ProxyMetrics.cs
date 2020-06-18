// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.Metrics
{
    internal class ProxyMetrics
    {
        private readonly Action<long, string, string, string, string, string> _streamCopyBytes;
        private readonly Action<long, string, string, string, string, string> _streamCopyIops;

        public ProxyMetrics(IMetricCreator metricCreator)
        {
            Contracts.CheckValue(metricCreator, nameof(metricCreator));

            _streamCopyBytes = metricCreator.Create("StreamCopyBytes", "direction", "clusterId", "routeId", "destinationId", "protocol");
            _streamCopyIops = metricCreator.Create("StreamCopyIops", "direction", "clusterId", "routeId", "destinationId", "protocol");
        }

        public void StreamCopyBytes(long value, string direction, string clusterId, string routeId, string destinationId, string protocol)
        {
            _streamCopyBytes(value, direction, clusterId, routeId, destinationId, protocol);
        }

        public void StreamCopyIops(long value, string direction, string clusterId, string routeId, string destinationId, string protocol)
        {
            _streamCopyIops(value, direction, clusterId, routeId, destinationId, protocol);
        }
    }
}

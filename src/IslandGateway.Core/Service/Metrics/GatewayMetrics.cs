// <copyright file="GatewayMetrics.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using IslandGateway.Common.Abstractions.Telemetry;
using IslandGateway.Utilities;

namespace IslandGateway.Core.Service.Metrics
{
    internal class GatewayMetrics
    {
        private readonly Action<long, string, string, string, string, string> _streamCopyBytes;
        private readonly Action<long, string, string, string, string, string> _streamCopyIops;

        public GatewayMetrics(IMetricCreator metricCreator)
        {
            Contracts.CheckValue(metricCreator, nameof(metricCreator));

            this._streamCopyBytes = metricCreator.Create("StreamCopyBytes", "direction", "backendId", "routeId", "endpointId", "protocol");
            this._streamCopyIops = metricCreator.Create("StreamCopyIops", "direction", "backendId", "routeId", "endpointId", "protocol");
        }

        public void StreamCopyBytes(long value, string direction, string backendId, string routeId, string endpointId, string protocol)
        {
            this._streamCopyBytes(value, direction, backendId, routeId, endpointId, protocol);
        }

        public void StreamCopyIops(long value, string direction, string backendId, string routeId, string endpointId, string protocol)
        {
            this._streamCopyIops(value, direction, backendId, routeId, endpointId, protocol);
        }
    }
}

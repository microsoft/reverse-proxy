// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Telemetry.Consumption;

namespace Yarp.ReverseProxy.Sample
{
    public sealed class ForwarderTelemetryConsumer : IForwarderTelemetryConsumer
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        private DateTime _startTime;

        public ForwarderTelemetryConsumer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void OnForwarderStart(DateTime timestamp, string destinationPrefix)
        {
            _startTime = timestamp;
        }

        public void OnForwarderStop(DateTime timestamp, int statusCode)
        {
            var elapsed =  timestamp - _startTime;
            var path = _httpContextAccessor.HttpContext.Request.Path;
            Console.WriteLine($"Spent {elapsed.TotalMilliseconds:N2} ms proxying {path}");
        }

        public void OnForwarderFailed(DateTime timestamp, ForwarderError error) { }

        public void OnForwarderStage(DateTime timestamp, ForwarderStage stage) { }

        public void OnContentTransferring(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime) { }

        public void OnContentTransferred(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime, TimeSpan firstReadTime) { }

        public void OnForwarderInvoke(DateTime timestamp, string clusterId, string routeId, string destinationId) { }
    }
}

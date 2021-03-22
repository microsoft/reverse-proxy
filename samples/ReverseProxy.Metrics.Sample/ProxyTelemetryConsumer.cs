// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Service.Proxy;
using Yarp.ReverseProxy.Telemetry.Consumption;

namespace Yarp.Sample
{
    public sealed class ProxyTelemetryConsumer : IProxyTelemetryConsumer
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        private DateTime _startTime;

        public ProxyTelemetryConsumer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void OnProxyStart(DateTime timestamp, string destinationPrefix)
        {
            _startTime = timestamp;
        }

        public void OnProxyStop(DateTime timestamp, int statusCode)
        {
            var elapsed =  timestamp - _startTime;
            var path = _httpContextAccessor.HttpContext.Request.Path;
            Console.WriteLine($"Spent {elapsed.TotalMilliseconds:N2} ms proxying {path}");
        }

        public void OnProxyFailed(DateTime timestamp, ProxyError error) { }

        public void OnProxyStage(DateTime timestamp, ProxyStage stage) { }

        public void OnContentTransferring(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime) { }

        public void OnContentTransferred(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime, TimeSpan firstReadTime) { }

        public void OnProxyInvoke(DateTime timestamp, string clusterId, string routeId, string destinationId) { }
    }
}

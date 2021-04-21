// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Service.Proxy;
using Yarp.ReverseProxy.Telemetry.Consumption;

namespace Yarp.Sample
{
    public sealed class ProxyTelemetryConsumer : IProxyTelemetryConsumer
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProxyTelemetryConsumer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void OnProxyStart(DateTime timestamp, string destinationPrefix)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.ProxyStartOffset = (timestamp - metrics.StartTime).Ticks;
        }

        public void OnProxyStop(DateTime timestamp, int statusCode)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.ProxyStopOffset = (timestamp - metrics.StartTime).Ticks;
        }

        public void OnProxyFailed(DateTime timestamp, ProxyError error)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.ProxyStopOffset = (timestamp - metrics.StartTime).Ticks;
            metrics.Error = error;
        }

        public void OnProxyStage(DateTime timestamp, ProxyStage stage) { }

        public void OnContentTransferring(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime) { }

        public void OnContentTransferred(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime, TimeSpan firstReadTime)
        {
            var metrics = PerRequestMetrics.Current;

            if (isRequest)
            {
                metrics.HttpRequestContentStopOffset = (timestamp - metrics.StartTime).Ticks;
                metrics.RequestBodyLength = contentLength;
                metrics.RequestContentIops = iops;
            }
            else
            {
                metrics.HttpResponseContentStopOffset = (timestamp - metrics.StartTime).Ticks;
                metrics.ResponseBodyLength = contentLength;
                metrics.ResponseContentIops = iops;
            }
        }

        public void OnProxyInvoke(DateTime timestamp, string clusterId, string routeId, string destinationId)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.RouteInvokeOffset = (timestamp - metrics.StartTime).Ticks;
            metrics.RouteId = routeId;
            metrics.ClusterId = clusterId;
            metrics.DestinationId = destinationId;
        }
    }
}

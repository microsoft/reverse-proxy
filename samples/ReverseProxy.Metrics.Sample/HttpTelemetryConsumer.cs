// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Service.Proxy;
using Yarp.ReverseProxy.Telemetry.Consumption;

namespace Yarp.Sample
{
#if NET5_0_OR_GREATER

    public sealed class HttpTelemetryConsumer : IHttpTelemetryConsumer
    {
        public void OnRequestStart(DateTime timestamp, string scheme, string host, int port, string pathAndQuery, int versionMajor, int versionMinor, HttpVersionPolicy versionPolicy)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestStartOffset = (timestamp - metrics.StartTime).Ticks;
        }

        public void OnRequestStop(DateTime timestamp)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestContentStopOffset = (timestamp - metrics.StartTime).Ticks;
        }

        public void OnRequestFailed(DateTime timestamp)
        {      
        }

        public void OnConnectionEstablished(DateTime timestamp, int versionMajor, int versionMinor)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpConnectionEstablishedOffset = (timestamp - metrics.StartTime).Ticks;
        }

        public void OnRequestLeftQueue(DateTime timestamp, TimeSpan timeOnQueue, int versionMajor, int versionMinor)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestLeftQueueOffset = (timestamp - metrics.StartTime).Ticks;
        }

        public void OnRequestHeadersStart(DateTime timestamp)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestHeadersStartOffset = (timestamp - metrics.StartTime).Ticks;
        }

        public void OnRequestHeadersStop(DateTime timestamp)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestHeadersStopOffset = (timestamp - metrics.StartTime).Ticks;
        }

        public void OnRequestContentStart(DateTime timestamp)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestContentStartOffset = (timestamp - metrics.StartTime).Ticks;
        }

        public void OnRequestContentStop(DateTime timestamp, long contentLength)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestContentStopOffset = (timestamp - metrics.StartTime).Ticks;
        }

        public void OnResponseHeadersStart(DateTime timestamp)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpResponseHeadersStartOffset = (timestamp - metrics.StartTime).Ticks;
        }

        public void OnResponseHeadersStop(DateTime timestamp)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpResponseHeadersStopOffset = (timestamp - metrics.StartTime).Ticks;
        }
    }
#endif
}

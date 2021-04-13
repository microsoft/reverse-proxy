// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    internal sealed class HttpEventListenerService : EventListener, IHostedService
    {
        private readonly ILogger<HttpEventListenerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private HttpMetrics _previousMetrics;
        private HttpMetrics _currentMetrics = new();
        private int _eventCountersCount;

        public HttpEventListenerService(ILogger<HttpEventListenerService> logger, IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "System.Net.Http")
            {
                var arguments = new Dictionary<string, string> { { "EventCounterIntervalSec", MetricsOptions.Interval.TotalSeconds.ToString() } };
                EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.None, arguments);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            const int MinEventId = 1;
            const int MaxEventId = 12;

            if (eventData.EventId < MinEventId || eventData.EventId > MaxEventId)
            {
                if (eventData.EventId == -1)
                {
                    OnEventCounters(eventData);
                }

                return;
            }

            var context = _httpContextAccessor?.HttpContext;
            if (context is null)
            {
                return;
            }

            using var consumers = context.RequestServices.GetServices<IHttpTelemetryConsumer>().GetEnumerator();

            if (!consumers.MoveNext())
            {
                return;
            }

            var payload = eventData.Payload;

            switch (eventData.EventId)
            {
                case 1:
                    Debug.Assert(eventData.EventName == "RequestStart" && payload.Count == 7);
                    {
                        var scheme = (string)payload[0];
                        var host = (string)payload[1];
                        var port = (int)payload[2];
                        var pathAndQuery = (string)payload[3];
                        var versionMajor = (int)(byte)payload[4];
                        var versionMinor = (int)(byte)payload[5];
                        var versionPolicy = (HttpVersionPolicy)payload[6];
                        do
                        {
                            consumers.Current.OnRequestStart(eventData.TimeStamp, scheme, host, port, pathAndQuery, versionMajor, versionMinor, versionPolicy);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 2:
                    Debug.Assert(eventData.EventName == "RequestStop" && payload.Count == 0);
                    {
                        do
                        {
                            consumers.Current.OnRequestStop(eventData.TimeStamp);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 3:
                    Debug.Assert(eventData.EventName == "RequestFailed" && payload.Count == 0);
                    {
                        do
                        {
                            consumers.Current.OnRequestFailed(eventData.TimeStamp);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 4:
                    Debug.Assert(eventData.EventName == "ConnectionEstablished" && payload.Count == 2);
                    {
                        var versionMajor = (int)(byte)payload[0];
                        var versionMinor = (int)(byte)payload[1];
                        do
                        {
                            consumers.Current.OnConnectionEstablished(eventData.TimeStamp, versionMajor, versionMinor);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 5:
                    Debug.Assert(eventData.EventName == "ConnectionClosed" && payload.Count == 2);
                    break;

                case 6:
                    Debug.Assert(eventData.EventName == "RequestLeftQueue" && payload.Count == 3);
                    {
                        var timeOnQueue = TimeSpan.FromMilliseconds((double)payload[0]);
                        var versionMajor = (int)(byte)payload[1];
                        var versionMinor = (int)(byte)payload[2];
                        do
                        {
                            consumers.Current.OnRequestLeftQueue(eventData.TimeStamp, timeOnQueue, versionMajor, versionMinor);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 7:
                    Debug.Assert(eventData.EventName == "RequestHeadersStart" && payload.Count == 0);
                    {
                        do
                        {
                            consumers.Current.OnRequestHeadersStart(eventData.TimeStamp);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 8:
                    Debug.Assert(eventData.EventName == "RequestHeadersStop" && payload.Count == 0);
                    {
                        do
                        {
                            consumers.Current.OnRequestHeadersStop(eventData.TimeStamp);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 9:
                    Debug.Assert(eventData.EventName == "RequestContentStart" && payload.Count == 0);
                    {
                        do
                        {
                            consumers.Current.OnRequestContentStart(eventData.TimeStamp);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 10:
                    Debug.Assert(eventData.EventName == "RequestContentStop" && payload.Count == 1);
                    {
                        var contentLength = (long)payload[0];
                        do
                        {
                            consumers.Current.OnRequestContentStop(eventData.TimeStamp, contentLength);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 11:
                    Debug.Assert(eventData.EventName == "ResponseHeadersStart" && payload.Count == 0);
                    {
                        do
                        {
                            consumers.Current.OnResponseHeadersStart(eventData.TimeStamp);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 12:
                    Debug.Assert(eventData.EventName == "ResponseHeadersStop" && payload.Count == 0);
                    {
                        do
                        {
                            consumers.Current.OnResponseHeadersStop(eventData.TimeStamp);
                        }
                        while (consumers.MoveNext());
                    }
                    break;
            }
        }

        private void OnEventCounters(EventWrittenEventArgs eventData)
        {
            Debug.Assert(eventData.EventName == "EventCounters" && eventData.Payload.Count == 1);
            var counters = (IDictionary<string, object>)eventData.Payload[0];

            if (!counters.TryGetValue("Mean", out var valueObj))
            {
                valueObj = counters["Increment"];
            }

            var value = (double)valueObj;
            var metrics = _currentMetrics;

            switch ((string)counters["Name"])
            {
                case "requests-started":
                    metrics.RequestsStarted = (long)value;
                    break;

                case "requests-started-rate":
                    metrics.RequestsStartedRate = (long)value;
                    break;

                case "requests-failed":
                    metrics.RequestsFailed = (long)value;
                    break;

                case "requests-failed-rate":
                    metrics.RequestsFailedRate = (long)value;
                    break;

                case "current-requests":
                    metrics.CurrentRequests = (long)value;
                    break;

                case "http11-connections-current-total":
                    metrics.CurrentHttp11Connections = (long)value;
                    break;

                case "http20-connections-current-total":
                    metrics.CurrentHttp20Connections = (long)value;
                    break;

                case "http11-requests-queue-duration":
                    metrics.Http11RequestsQueueDuration = TimeSpan.FromMilliseconds(value);
                    break;

                case "http20-requests-queue-duration":
                    metrics.Http20RequestsQueueDuration = TimeSpan.FromMilliseconds(value);
                    break;

                default:
                    return;
            }

            const int TotalEventCounters = 9;

            if (++_eventCountersCount == TotalEventCounters)
            {
                _eventCountersCount = 0;

                metrics.Timestamp = DateTime.UtcNow;

                var previous = _previousMetrics;
                _previousMetrics = metrics;
                _currentMetrics = new HttpMetrics();

                if (previous is null || _serviceProvider is null)
                {
                    return;
                }

                try
                {
                    foreach (var consumer in _serviceProvider.GetServices<IHttpMetricsConsumer>())
                    {
                        consumer.OnHttpMetrics(previous, metrics);
                    }
                }
                catch (Exception ex)
                {
                    // We can't let an uncaught exception propagate as that would crash the process
                    _logger.LogError(ex, $"Uncaught exception occured while processing {nameof(HttpMetrics)}.");
                }
            }
        }
    }
}

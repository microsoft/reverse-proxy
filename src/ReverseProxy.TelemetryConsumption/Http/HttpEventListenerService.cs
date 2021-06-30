// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    internal sealed class HttpEventListenerService : EventListenerService<HttpEventListenerService, IHttpTelemetryConsumer, IHttpMetricsConsumer>
    {
        private HttpMetrics _previousMetrics;
        private HttpMetrics _currentMetrics = new();
        private int _eventCountersCount;

        protected override string EventSourceName => "System.Net.Http";

        public HttpEventListenerService(ILogger<HttpEventListenerService> logger, IEnumerable<IHttpTelemetryConsumer> telemetryConsumers, IEnumerable<IHttpMetricsConsumer> metricsConsumers)
            : base(logger, telemetryConsumers, metricsConsumers)
        { }

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

            if (TelemetryConsumers is null)
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
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnRequestStart(eventData.TimeStamp, scheme, host, port, pathAndQuery, versionMajor, versionMinor, versionPolicy);
                        }
                    }
                    break;

                case 2:
                    Debug.Assert(eventData.EventName == "RequestStop" && payload.Count == 0);
                    {
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnRequestStop(eventData.TimeStamp);
                        }
                    }
                    break;

                case 3:
                    Debug.Assert(eventData.EventName == "RequestFailed" && payload.Count == 0);
                    {
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnRequestFailed(eventData.TimeStamp);
                        }
                    }
                    break;

                case 4:
                    Debug.Assert(eventData.EventName == "ConnectionEstablished" && payload.Count == 2);
                    {
                        var versionMajor = (int)(byte)payload[0];
                        var versionMinor = (int)(byte)payload[1];
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnConnectionEstablished(eventData.TimeStamp, versionMajor, versionMinor);
                        }
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
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnRequestLeftQueue(eventData.TimeStamp, timeOnQueue, versionMajor, versionMinor);
                        }
                    }
                    break;

                case 7:
                    Debug.Assert(eventData.EventName == "RequestHeadersStart" && payload.Count == 0);
                    {
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnRequestHeadersStart(eventData.TimeStamp);
                        }
                    }
                    break;

                case 8:
                    Debug.Assert(eventData.EventName == "RequestHeadersStop" && payload.Count == 0);
                    {
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnRequestHeadersStop(eventData.TimeStamp);
                        }
                    }
                    break;

                case 9:
                    Debug.Assert(eventData.EventName == "RequestContentStart" && payload.Count == 0);
                    {
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnRequestContentStart(eventData.TimeStamp);
                        }
                    }
                    break;

                case 10:
                    Debug.Assert(eventData.EventName == "RequestContentStop" && payload.Count == 1);
                    {
                        var contentLength = (long)payload[0];
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnRequestContentStop(eventData.TimeStamp, contentLength);
                        }
                    }
                    break;

                case 11:
                    Debug.Assert(eventData.EventName == "ResponseHeadersStart" && payload.Count == 0);
                    {
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnResponseHeadersStart(eventData.TimeStamp);
                        }
                    }
                    break;

                case 12:
                    Debug.Assert(eventData.EventName == "ResponseHeadersStop" && payload.Count == 0);
                    {
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnResponseHeadersStop(eventData.TimeStamp);
                        }
                    }
                    break;
            }
        }

        private void OnEventCounters(EventWrittenEventArgs eventData)
        {
            if (MetricsConsumers is null)
            {
                return;
            }

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

                if (previous is null)
                {
                    return;
                }

                try
                {
                    foreach (var consumer in MetricsConsumers)
                    {
                        consumer.OnHttpMetrics(previous, metrics);
                    }
                }
                catch (Exception ex)
                {
                    // We can't let an uncaught exception propagate as that would crash the process
                    Logger.LogError(ex, $"Uncaught exception occured while processing {nameof(HttpMetrics)}.");
                }
            }
        }
    }
}

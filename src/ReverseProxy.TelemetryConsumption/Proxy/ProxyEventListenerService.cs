// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Service.Proxy;

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    internal sealed class ProxyEventListenerService : EventListenerService<ProxyEventListenerService, IProxyTelemetryConsumer, IProxyMetricsConsumer>
    {
        private ProxyMetrics _previousMetrics;
        private ProxyMetrics _currentMetrics = new();
        private int _eventCountersCount;

        protected override string EventSourceName => "Yarp.ReverseProxy";

        public ProxyEventListenerService(ILogger<ProxyEventListenerService> logger, IEnumerable<IProxyTelemetryConsumer> telemetryConsumers, IEnumerable<IProxyMetricsConsumer> metricsConsumers)
            : base(logger, telemetryConsumers, metricsConsumers)
        { }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            const int MinEventId = 1;
            const int MaxEventId = 7;

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
                    Debug.Assert(eventData.EventName == "ProxyStart" && payload.Count == 1);
                    {
                        var destinationPrefix = (string)payload[0];
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnProxyStart(eventData.TimeStamp, destinationPrefix);
                        }
                    }
                    break;

                case 2:
                    Debug.Assert(eventData.EventName == "ProxyStop" && payload.Count == 1);
                    {
                        var statusCode = (int)payload[0];
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnProxyStop(eventData.TimeStamp, statusCode);
                        }
                    }
                    break;

                case 3:
                    Debug.Assert(eventData.EventName == "ProxyFailed" && payload.Count == 1);
                    {
                        var error = (ProxyError)payload[0];
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnProxyFailed(eventData.TimeStamp, error);
                        }
                    }
                    break;

                case 4:
                    Debug.Assert(eventData.EventName == "ProxyStage" && payload.Count == 1);
                    {
                        var proxyStage = (ProxyStage)payload[0];
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnProxyStage(eventData.TimeStamp, proxyStage);
                        }
                    }
                    break;

                case 5:
                    Debug.Assert(eventData.EventName == "ContentTransferring" && payload.Count == 5);
                    {
                        var isRequest = (bool)payload[0];
                        var contentLength = (long)payload[1];
                        var iops = (long)payload[2];
                        var readTime = new TimeSpan((long)payload[3]);
                        var writeTime = new TimeSpan((long)payload[4]);
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnContentTransferring(eventData.TimeStamp, isRequest, contentLength, iops, readTime, writeTime);
                        }
                    }
                    break;

                case 6:
                    Debug.Assert(eventData.EventName == "ContentTransferred" && payload.Count == 6);
                    {
                        var isRequest = (bool)payload[0];
                        var contentLength = (long)payload[1];
                        var iops = (long)payload[2];
                        var readTime = new TimeSpan((long)payload[3]);
                        var writeTime = new TimeSpan((long)payload[4]);
                        var firstReadTime = new TimeSpan((long)payload[5]);
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnContentTransferred(eventData.TimeStamp, isRequest, contentLength, iops, readTime, writeTime, firstReadTime);
                        }
                    }
                    break;

                case 7:
                    Debug.Assert(eventData.EventName == "ProxyInvoke" && payload.Count == 3);
                    {
                        var clusterId = (string)payload[0];
                        var routeId = (string)payload[1];
                        var destinationId = (string)payload[2];
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnProxyInvoke(eventData.TimeStamp, clusterId, routeId, destinationId);
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

            var value = (long)(double)valueObj;
            var metrics = _currentMetrics;

            switch ((string)counters["Name"])
            {
                case "requests-started":
                    metrics.RequestsStarted = value;
                    break;

                case "requests-started-rate":
                    metrics.RequestsStartedRate = value;
                    break;

                case "requests-failed":
                    metrics.RequestsFailed = value;
                    break;

                case "current-requests":
                    metrics.CurrentRequests = value;
                    break;

                default:
                    return;
            }

            const int TotalEventCounters = 4;

            if (++_eventCountersCount == TotalEventCounters)
            {
                _eventCountersCount = 0;

                metrics.Timestamp = DateTime.UtcNow;

                var previous = _previousMetrics;
                _previousMetrics = metrics;
                _currentMetrics = new ProxyMetrics();

                if (previous is null)
                {
                    return;
                }

                try
                {
                    foreach (var consumer in MetricsConsumers)
                    {
                        consumer.OnProxyMetrics(previous, metrics);
                    }
                }
                catch (Exception ex)
                {
                    // We can't let an uncaught exception propagate as that would crash the process
                    Logger.LogError(ex, $"Uncaught exception occured while processing {nameof(ProxyMetrics)}.");
                }
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    internal sealed class NameResolutionEventListenerService : EventListenerService<NameResolutionEventListenerService, INameResolutionTelemetryConsumer, INameResolutionMetricsConsumer>
    {
        private NameResolutionMetrics _previousMetrics;
        private NameResolutionMetrics _currentMetrics = new();
        private int _eventCountersCount;

        protected override string EventSourceName => "System.Net.NameResolution";

        public NameResolutionEventListenerService(ILogger<NameResolutionEventListenerService> logger, IEnumerable<INameResolutionTelemetryConsumer> telemetryConsumers, IEnumerable<INameResolutionMetricsConsumer> metricsConsumers)
            : base(logger, telemetryConsumers, metricsConsumers)
        { }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            const int MinEventId = 1;
            const int MaxEventId = 3;

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
                    Debug.Assert(eventData.EventName == "ResolutionStart" && payload.Count == 1);
                    {
                        var hostNameOrAddress = (string)payload[0];
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnResolutionStart(eventData.TimeStamp, hostNameOrAddress);
                        }
                    }
                    break;

                case 2:
                    Debug.Assert(eventData.EventName == "ResolutionStop" && payload.Count == 0);
                    {
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnResolutionStop(eventData.TimeStamp);
                        }
                    }
                    break;

                case 3:
                    Debug.Assert(eventData.EventName == "ResolutionFailed" && payload.Count == 0);
                    {
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnResolutionFailed(eventData.TimeStamp);
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
                case "dns-lookups-requested":
                    metrics.DnsLookupsRequested = (long)value;
                    break;

                case "dns-lookups-duration":
                    metrics.AverageLookupDuration = TimeSpan.FromMilliseconds(value);
                    break;

                default:
                    return;
            }

            const int TotalEventCounters = 2;

            if (++_eventCountersCount == TotalEventCounters)
            {
                _eventCountersCount = 0;

                metrics.Timestamp = DateTime.UtcNow;

                var previous = _previousMetrics;
                _previousMetrics = metrics;
                _currentMetrics = new NameResolutionMetrics();

                if (previous is null)
                {
                    return;
                }

                try
                {
                    foreach (var consumer in MetricsConsumers)
                    {
                        consumer.OnNameResolutionMetrics(previous, metrics);
                    }
                }
                catch (Exception ex)
                {
                    // We can't let an uncaught exception propagate as that would crash the process
                    Logger.LogError(ex, $"Uncaught exception occured while processing {nameof(NameResolutionMetrics)}.");
                }
            }
        }
    }
}

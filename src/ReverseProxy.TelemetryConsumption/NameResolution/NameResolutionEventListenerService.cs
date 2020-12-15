// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    internal sealed class NameResolutionEventListenerService : EventListener, IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private NameResolutionMetrics _previousMetrics;
        private NameResolutionMetrics _currentMetrics = new();
        private int _eventCountersCount;

        public NameResolutionEventListenerService(IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "System.Net.NameResolution")
            {
                var arguments = new Dictionary<string, string> { { "EventCounterIntervalSec", MetricsOptions.IntervalSeconds.ToString() } };
                EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.None, arguments);
            }
        }

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

            var context = _httpContextAccessor?.HttpContext;
            if (context is null)
            {
                return;
            }

            using var consumers = context.RequestServices.GetServices<INameResolutionTelemetryConsumer>().GetEnumerator();

            if (!consumers.MoveNext())
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
                        do
                        {
                            consumers.Current.OnResolutionStart(eventData.TimeStamp, hostNameOrAddress);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 2:
                    Debug.Assert(eventData.EventName == "ResolutionStop" && payload.Count == 0);
                    {
                        do
                        {
                            consumers.Current.OnResolutionStop(eventData.TimeStamp);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 3:
                    Debug.Assert(eventData.EventName == "ResolutionFailed" && payload.Count == 0);
                    {
                        do
                        {
                            consumers.Current.OnResolutionFailed(eventData.TimeStamp);
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

                if (previous is null || _serviceProvider is null)
                {
                    return;
                }

                foreach (var consumer in _serviceProvider.GetServices<INameResolutionMetricsConsumer>())
                {
                    consumer.OnNameResolutionMetrics(previous, metrics);
                }
            }
        }
    }
}

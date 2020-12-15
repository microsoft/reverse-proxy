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
    internal sealed class KestrelEventListenerService : EventListener, IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private KestrelMetrics _previousMetrics;
        private KestrelMetrics _currentMetrics = new();
        private int _eventCountersCount;

        public KestrelEventListenerService(IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft-AspNetCore-Server-Kestrel")
            {
                var arguments = new Dictionary<string, string> { { "EventCounterIntervalSec", MetricsOptions.IntervalSeconds.ToString() } };
                EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.None, arguments);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventId < 1)
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

            using var consumers = context.RequestServices.GetServices<IKestrelTelemetryConsumer>().GetEnumerator();

            if (!consumers.MoveNext())
            {
                return;
            }

            var payload = eventData.Payload;

            switch (eventData.EventId)
            {
                case 3:
                    Debug.Assert(eventData.EventName == "RequestStart" && payload.Count == 5);
                    {
                        var connectionId = (string)payload[0];
                        var requestId = (string)payload[1];
                        var httpVersion = (string)payload[2];
                        var path = (string)payload[3];
                        var method = (string)payload[4];
                        do
                        {
                            consumers.Current.OnRequestStart(eventData.TimeStamp, connectionId, requestId, httpVersion, path, method);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 4:
                    Debug.Assert(eventData.EventName == "RequestStop" && payload.Count == 5);
                    {
                        var connectionId = (string)payload[0];
                        var requestId = (string)payload[1];
                        var httpVersion = (string)payload[2];
                        var path = (string)payload[3];
                        var method = (string)payload[4];
                        do
                        {
                            consumers.Current.OnRequestStop(eventData.TimeStamp, connectionId, requestId, httpVersion, path, method);
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

            var value = (long)(double)valueObj;
            var metrics = _currentMetrics;

            switch ((string)counters["Name"])
            {
                case "connections-per-second":
                    metrics.ConnectionRate = value;
                    break;

                case "total-connections":
                    metrics.TotalConnections = value;
                    break;

                case "tls-handshakes-per-second":
                    metrics.TlsHandshakeRate = value;
                    break;

                case "total-tls-handshakes":
                    metrics.TotalTlsHandshakes = value;
                    break;

                case "current-tls-handshakes":
                    metrics.CurrentTlsHandshakes = value;
                    break;

                case "failed-tls-handshakes":
                    metrics.FailedTlsHandshakes = value;
                    break;

                case "current-connections":
                    metrics.CurrentConnections = value;
                    break;

                case "connection-queue-length":
                    metrics.ConnectionQueueLength = value;
                    break;

                case "request-queue-length":
                    metrics.RequestQueueLength = value;
                    break;

                case "current-upgraded-requests":
                    metrics.CurrentUpgradedRequests = value;
                    break;

                default:
                    return;
            }

            const int TotalEventCounters = 10;

            if (++_eventCountersCount == TotalEventCounters)
            {
                _eventCountersCount = 0;

                metrics.Timestamp = DateTime.UtcNow;

                var previous = _previousMetrics;
                _previousMetrics = metrics;
                _currentMetrics = new KestrelMetrics();

                if (previous is null || _serviceProvider is null)
                {
                    return;
                }

                foreach (var consumer in _serviceProvider.GetServices<IKestrelMetricsConsumer>())
                {
                    consumer.OnKestrelMetrics(previous, metrics);
                }
            }
        }
    }
}

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
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    internal sealed class KestrelEventListenerService : EventListener, IHostedService
    {
#if NET5_0
        private readonly ILogger<KestrelEventListenerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private KestrelMetrics _previousMetrics;
        private KestrelMetrics _currentMetrics = new();
        private int _eventCountersCount;

        public KestrelEventListenerService(ILogger<KestrelEventListenerService> logger, IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }
#else
        private readonly IHttpContextAccessor _httpContextAccessor;

        public KestrelEventListenerService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }
#endif

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft-AspNetCore-Server-Kestrel")
            {
                var arguments = new Dictionary<string, string> { { "EventCounterIntervalSec", MetricsOptions.Interval.TotalSeconds.ToString() } };
                EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.None, arguments);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            const int MinEventId = 3;
            const int MaxEventId = 4;

            if (eventData.EventId < MinEventId || eventData.EventId > MaxEventId)
            {
#if NET5_0
                if (eventData.EventId == -1)
                {
                    OnEventCounters(eventData);
                }
#endif

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

#if NET5_0
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
#else
            switch (eventData.EventId)
            {
                case 3:
                    Debug.Assert(eventData.EventName == "RequestStart" && payload.Count == 2);
                    {
                        var connectionId = (string)payload[0];
                        var requestId = (string)payload[1];
                        do
                        {
                            consumers.Current.OnRequestStart(eventData.TimeStamp, connectionId, requestId);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 4:
                    Debug.Assert(eventData.EventName == "RequestStop" && payload.Count == 2);
                    {
                        var connectionId = (string)payload[0];
                        var requestId = (string)payload[1];
                        do
                        {
                            consumers.Current.OnRequestStop(eventData.TimeStamp, connectionId, requestId);
                        }
                        while (consumers.MoveNext());
                    }
                    break;
            }
#endif
        }

#if NET5_0
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

                try
                {
                    foreach (var consumer in _serviceProvider.GetServices<IKestrelMetricsConsumer>())
                    {
                        consumer.OnKestrelMetrics(previous, metrics);
                    }
                }
                catch (Exception ex)
                {
                    // We can't let an uncaught exception propagate as that would crash the process
                    _logger.LogError(ex, $"Uncaught exception occured while processing {nameof(KestrelMetrics)}.");
                }
            }
        }
#endif
    }
}

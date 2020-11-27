// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    internal sealed class NetSecurityEventListenerService : EventListener, IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private NetSecurityMetrics _previousMetrics;
        private NetSecurityMetrics _currentMetrics = new();
        private int _eventCountersCount;

        public NetSecurityEventListenerService(IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "System.Net.Security")
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

            var context = _httpContextAccessor.HttpContext;
            if (context is null)
            {
                return;
            }

            using var consumers = context.RequestServices.GetServices<INetSecurityTelemetryConsumer>().GetEnumerator();

            if (!consumers.MoveNext())
            {
                return;
            }

            var payload = eventData.Payload;

            switch (eventData.EventId)
            {
                case 1:
                    Debug.Assert(eventData.EventName == "HandshakeStart" && payload.Count == 2);
                    {
                        var isServer = (bool)payload[0];
                        var targetHost = (string)payload[1];
                        do
                        {
                            consumers.Current.OnHandshakeStart(eventData.TimeStamp, isServer, targetHost);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 2:
                    Debug.Assert(eventData.EventName == "HandshakeStop" && payload.Count == 1);
                    {
                        var protocol = (SslProtocols)payload[0];
                        do
                        {
                            consumers.Current.OnHandshakeStop(eventData.TimeStamp, protocol);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 3:
                    Debug.Assert(eventData.EventName == "HandshakeFailed" && payload.Count == 3);
                    {
                        var isServer = (bool)payload[0];
                        var elapsed = TimeSpan.FromMilliseconds((double)payload[1]);
                        var exceptionMessage = (string)payload[2];
                        do
                        {
                            consumers.Current.OnHandshakeFailed(eventData.TimeStamp, isServer, elapsed, exceptionMessage);
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
                case "tls-handshake-rate":
                    metrics.TlsHandshakeRate = (long)value;
                    break;

                case "total-tls-handshakes":
                    metrics.TotalTlsHandshakes = (long)value;
                    break;

                case "current-tls-handshakes":
                    metrics.CurrentTlsHandshakes = (long)value;
                    break;

                case "failed-tls-handshakes":
                    metrics.FailedTlsHandshakes = (long)value;
                    break;

                case "all-tls-sessions-open":
                    metrics.TlsSessionsOpen = (long)value;
                    break;

                case "tls10-sessions-open":
                    metrics.Tls10SessionsOpen = (long)value;
                    break;

                case "tls11-sessions-open":
                    metrics.Tls11SessionsOpen = (long)value;
                    break;

                case "tls12-sessions-open":
                    metrics.Tls12SessionsOpen = (long)value;
                    break;

                case "tls13-sessions-open":
                    metrics.Tls13SessionsOpen = (long)value;
                    break;

                case "all-tls-handshake-duration":
                    metrics.TlsHandshakeDuration = TimeSpan.FromMilliseconds(value);
                    break;

                case "tls10-handshake-duration":
                    metrics.Tls10HandshakeDuration = TimeSpan.FromMilliseconds(value);
                    break;

                case "tls11-handshake-duration":
                    metrics.Tls11HandshakeDuration = TimeSpan.FromMilliseconds(value);
                    break;

                case "tls12-handshake-duration":
                    metrics.Tls12HandshakeDuration = TimeSpan.FromMilliseconds(value);
                    break;

                case "tls13-handshake-duration":
                    metrics.Tls13HandshakeDuration = TimeSpan.FromMilliseconds(value);
                    break;

                default:
                    return;
            }

            const int TotalEventCounters = 14;

            if (++_eventCountersCount == TotalEventCounters)
            {
                _eventCountersCount = 0;

                metrics.Timestamp = DateTime.UtcNow;

                var previous = _previousMetrics;
                _previousMetrics = metrics;
                _currentMetrics = new NetSecurityMetrics();

                if (previous is null)
                {
                    return;
                }

                foreach (var consumer in _serviceProvider.GetServices<INetSecurityMetricsConsumer>())
                {
                    consumer.OnNetSecurityMetrics(previous, metrics);
                }
            }
        }
    }
}

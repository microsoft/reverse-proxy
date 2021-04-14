// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    internal sealed class SocketsEventListenerService : EventListenerService<SocketsEventListenerService, ISocketsTelemetryConsumer, ISocketsMetricsConsumer>
    {
        private SocketsMetrics _previousMetrics;
        private SocketsMetrics _currentMetrics = new();
        private int _eventCountersCount;

        protected override string EventSourceName => "System.Net.Sockets";

        public SocketsEventListenerService(ILogger<SocketsEventListenerService> logger, IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor, ServiceCollectionInternal services)
            : base(logger, serviceProvider, httpContextAccessor, services)
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

            var context = HttpContextAccessor.HttpContext;
            if (context is null)
            {
                return;
            }

            using var consumers = context.RequestServices.GetServices<ISocketsTelemetryConsumer>().GetEnumerator();

            if (!consumers.MoveNext())
            {
                return;
            }

            var payload = eventData.Payload;

            switch (eventData.EventId)
            {
                case 1:
                    Debug.Assert(eventData.EventName == "ConnectStart" && payload.Count == 1);
                    {
                        var address = (string)payload[0];
                        do
                        {
                            consumers.Current.OnConnectStart(eventData.TimeStamp, address);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 2:
                    Debug.Assert(eventData.EventName == "ConnectStop" && payload.Count == 0);
                    {
                        do
                        {
                            consumers.Current.OnConnectStop(eventData.TimeStamp);
                        }
                        while (consumers.MoveNext());
                    }
                    break;

                case 3:
                    Debug.Assert(eventData.EventName == "ConnectFailed" && payload.Count == 2);
                    {
                        var error = (SocketError)payload[0];
                        var exceptionMessage = (string)payload[1];
                        do
                        {
                            consumers.Current.OnConnectFailed(eventData.TimeStamp, error, exceptionMessage);
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
                case "outgoing-connections-established":
                    metrics.OutgoingConnectionsEstablished = value;
                    break;

                case "incoming-connections-established":
                    metrics.IncomingConnectionsEstablished = value;
                    break;

                case "bytes-received":
                    metrics.BytesReceived = value;
                    break;

                case "bytes-sent":
                    metrics.BytesSent = value;
                    break;

                case "datagrams-received":
                    metrics.DatagramsReceived = value;
                    break;

                case "datagrams-sent":
                    metrics.DatagramsSent = value;
                    break;

                default:
                    return;
            }

            const int TotalEventCounters = 6;

            if (++_eventCountersCount == TotalEventCounters)
            {
                _eventCountersCount = 0;

                metrics.Timestamp = DateTime.UtcNow;

                var previous = _previousMetrics;
                _previousMetrics = metrics;
                _currentMetrics = new SocketsMetrics();

                if (previous is null)
                {
                    return;
                }

                try
                {
                    foreach (var consumer in ServiceProvider.GetServices<ISocketsMetricsConsumer>())
                    {
                        consumer.OnSocketsMetrics(previous, metrics);
                    }
                }
                catch (Exception ex)
                {
                    // We can't let an uncaught exception propagate as that would crash the process
                    Logger.LogError(ex, $"Uncaught exception occured while processing {nameof(SocketsMetrics)}.");
                }
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Yarp.Telemetry.Consumption
{
    internal interface IWebSocketsMetricsConsumer { }

    internal sealed class WebSocketsEventListenerService : EventListenerService<WebSocketsEventListenerService, IWebSocketsTelemetryConsumer, IWebSocketsMetricsConsumer>
    {
        protected override string EventSourceName => "Yarp.ReverseProxy.WebSockets";

        public WebSocketsEventListenerService(ILogger<WebSocketsEventListenerService> logger, IEnumerable<IWebSocketsTelemetryConsumer> telemetryConsumers, IEnumerable<IWebSocketsMetricsConsumer> metricsConsumers)
            : base(logger, telemetryConsumers, metricsConsumers)
        { }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            const int MinEventId = 1;
            const int MaxEventId = 1;

            if (eventData.EventId < MinEventId || eventData.EventId > MaxEventId)
            {
                return;
            }

            if (TelemetryConsumers is null)
            {
                return;
            }

#pragma warning disable IDE0007 // Use implicit type
            // Explicit type here to drop the object? signature of payload elements
            ReadOnlyCollection<object> payload = eventData.Payload!;
#pragma warning restore IDE0007 // Use implicit type

            switch (eventData.EventId)
            {
                case 1:
                    Debug.Assert(eventData.EventName == "WebSocketClosed" && payload.Count == 4);
                    {
                        var establishedTime = new DateTime((long)payload[0]);
                        var closeReason = (WebSocketCloseReason)payload[1];
                        var messagesRead = (long)payload[2];
                        var messagesWritten = (long)payload[3];
                        foreach (var consumer in TelemetryConsumers)
                        {
                            consumer.OnWebSocketClosed(eventData.TimeStamp, establishedTime, closeReason, messagesRead, messagesWritten);
                        }
                    }
                    break;
            }
        }
    }
}

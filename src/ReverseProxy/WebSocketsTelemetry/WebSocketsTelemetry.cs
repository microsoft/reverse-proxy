// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.Tracing;

namespace Yarp.ReverseProxy.WebSocketsTelemetry;

[EventSource(Name = "Yarp.ReverseProxy.WebSockets")]
internal sealed class WebSocketsTelemetry : EventSource
{
    public static readonly WebSocketsTelemetry Log = new();

    [Event(1, Level = EventLevel.Informational)]
    public void WebSocketClosed(long establishedTime, WebSocketCloseReason closeReason, long messagesRead, long messagesWritten)
    {
        if (IsEnabled(EventLevel.Informational, EventKeywords.All))
        {
            WriteEvent(eventId: 1, establishedTime, closeReason, messagesRead, messagesWritten);
        }
    }
}

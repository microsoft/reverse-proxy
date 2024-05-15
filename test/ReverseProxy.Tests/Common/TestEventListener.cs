// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading;

namespace Yarp.Tests.Common;

internal static class TestEventListener
{
    private static readonly AsyncLocal<List<EventWrittenEventArgs>> _eventsAsyncLocal = new();
#pragma warning disable IDE0052 // Remove unread private members
    private static readonly InternalEventListener _listener = new();
#pragma warning restore IDE0052

    public static List<EventWrittenEventArgs> Collect() => _eventsAsyncLocal.Value ??= [];

    private sealed class InternalEventListener : EventListener
    {
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Yarp.ReverseProxy")
            {
                EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData) =>
            _eventsAsyncLocal.Value?.Add(eventData);
    }
}

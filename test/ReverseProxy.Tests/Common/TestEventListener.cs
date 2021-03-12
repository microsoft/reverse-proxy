// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading;

namespace Yarp.ReverseProxy.Common.Tests
{
    internal static class TestEventListener
    {
        private static readonly AsyncLocal<List<EventWrittenEventArgs>> _eventsAsyncLocal = new AsyncLocal<List<EventWrittenEventArgs>>();
        private static readonly InternalEventListener _listener = new InternalEventListener();

        public static List<EventWrittenEventArgs> Collect()
        {
            return _eventsAsyncLocal.Value = new List<EventWrittenEventArgs>();
        }

        private sealed class InternalEventListener : EventListener
        {
            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "Yarp.ReverseProxy")
                {
                    EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (eventData.EventId == 0)
                {
                    throw new Exception($"EventSource error received: {eventData.Payload[0]}");
                }

                _eventsAsyncLocal.Value?.Add(eventData);
            }
        }
    }
}

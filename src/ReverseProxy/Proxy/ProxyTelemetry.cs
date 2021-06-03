// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;

namespace Yarp.ReverseProxy.Proxy
{
    internal sealed class ProxyTelemetry : EventSource
    {
        public static readonly ProxyTelemetry Log = new ProxyTelemetry();

        private IncrementingPollingCounter? _startedRequestsPerSecondCounter;
        private PollingCounter? _startedRequestsCounter;
        private PollingCounter? _currentRequestsCounter;
        private PollingCounter? _failedRequestsCounter;

        private long _startedRequests;
        private long _stoppedRequests;
        private long _failedRequests;

        private ProxyTelemetry()
            : base("Yarp.ReverseProxy")
        { }

        [Event(1, Level = EventLevel.Informational)]
        public void ProxyStart(string destinationPrefix)
        {
            Interlocked.Increment(ref _startedRequests);

            if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            {
                WriteEvent(eventId: 1, destinationPrefix);
            }
        }

        [Event(2, Level = EventLevel.Informational)]
        public void ProxyStop(int statusCode)
        {
            Interlocked.Increment(ref _stoppedRequests);

            if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            {
                WriteEvent(eventId: 2, statusCode);
            }
        }

        [Event(3, Level = EventLevel.Informational)]
        public void ProxyFailed(ProxyError error)
        {
            Interlocked.Increment(ref _failedRequests);

            if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            {
                Debug.Assert(sizeof(ProxyError) == sizeof(int), "Backing type of ProxyError MUST NOT be changed");
                WriteEvent(eventId: 3, (int)error);
            }
        }

        [Event(4, Level = EventLevel.Informational)]
        public void ProxyStage(ProxyStage stage)
        {
            if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            {
                Debug.Assert(sizeof(ProxyStage) == sizeof(int), "Backing type of ProxyStage MUST NOT be changed");
                WriteEvent(eventId: 4, (int)stage);
            }
        }

        [Event(5, Level = EventLevel.Informational)]
        public void ContentTransferring(bool isRequest, long contentLength, long iops, long readTime, long writeTime)
        {
            if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            {
                WriteEvent(eventId: 5, isRequest, contentLength, iops, readTime, writeTime);
            }
        }

        [Event(6, Level = EventLevel.Informational)]
        public void ContentTransferred(bool isRequest, long contentLength, long iops, long readTime, long writeTime, long firstReadTime)
        {
            if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            {
                WriteEvent(eventId: 6, isRequest, contentLength, iops, readTime, writeTime, firstReadTime);
            }
        }

        [Event(7, Level = EventLevel.Informational)]
        public void ProxyInvoke(string clusterId, string routeId, string destinationId)
        {
            if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            {
                WriteEvent(eventId: 7, clusterId, routeId, destinationId);
            }
        }


        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                _startedRequestsCounter ??= new PollingCounter("requests-started", this, () => Volatile.Read(ref _startedRequests))
                {
                    DisplayName = "Requests Started",
                };

                _startedRequestsPerSecondCounter ??= new IncrementingPollingCounter("requests-started-rate", this, () => Volatile.Read(ref _startedRequests))
                {
                    DisplayName = "Requests Started Rate",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                };

                _failedRequestsCounter ??= new PollingCounter("requests-failed", this, () => Volatile.Read(ref _failedRequests))
                {
                    DisplayName = "Requests Failed"
                };

                _currentRequestsCounter ??= new PollingCounter("current-requests", this, () => -Volatile.Read(ref _stoppedRequests) + Volatile.Read(ref _startedRequests))
                {
                    DisplayName = "Current Requests"
                };
            }
        }


        [NonEvent]
        private unsafe void WriteEvent(int eventId, bool arg1, long arg2, long arg3, long arg4, long arg5)
        {
            const int NumEventDatas = 5;
            var descrs = stackalloc EventData[NumEventDatas];

            descrs[0] = new EventData
            {
                DataPointer = (IntPtr)(&arg1),
                Size = sizeof(int) // EventSource defines bool as a 32-bit type
            };
            descrs[1] = new EventData
            {
                DataPointer = (IntPtr)(&arg2),
                Size = sizeof(long)
            };
            descrs[2] = new EventData
            {
                DataPointer = (IntPtr)(&arg3),
                Size = sizeof(long)
            };
            descrs[3] = new EventData
            {
                DataPointer = (IntPtr)(&arg4),
                Size = sizeof(long)
            };
            descrs[4] = new EventData
            {
                DataPointer = (IntPtr)(&arg5),
                Size = sizeof(long)
            };

            WriteEventCore(eventId, NumEventDatas, descrs);
        }

        [NonEvent]
        private unsafe void WriteEvent(int eventId, bool arg1, long arg2, long arg3, long arg4, long arg5, long arg6)
        {
            const int NumEventDatas = 6;
            var descrs = stackalloc EventData[NumEventDatas];

            descrs[0] = new EventData
            {
                DataPointer = (IntPtr)(&arg1),
                Size = sizeof(int) // EventSource defines bool as a 32-bit type
            };
            descrs[1] = new EventData
            {
                DataPointer = (IntPtr)(&arg2),
                Size = sizeof(long)
            };
            descrs[2] = new EventData
            {
                DataPointer = (IntPtr)(&arg3),
                Size = sizeof(long)
            };
            descrs[3] = new EventData
            {
                DataPointer = (IntPtr)(&arg4),
                Size = sizeof(long)
            };
            descrs[4] = new EventData
            {
                DataPointer = (IntPtr)(&arg5),
                Size = sizeof(long)
            };
            descrs[5] = new EventData
            {
                DataPointer = (IntPtr)(&arg6),
                Size = sizeof(long)
            };

            WriteEventCore(eventId, NumEventDatas, descrs);
        }
    }
}

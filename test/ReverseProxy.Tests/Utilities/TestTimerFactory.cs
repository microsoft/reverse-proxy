// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace Microsoft.ReverseProxy.Utilities
{
    internal class TestTimerFactory : ITimerFactory, IDisposable
    {
        private readonly List<(Timer Timer, AutoResetEvent Event, long DueTime)> _timers = new List<(Timer Timer, AutoResetEvent Event, long DueTime)>();

        public int Count => _timers.Count;

        public void FireTimer(int idx)
        {
            _timers[idx].Timer.Change(0, Timeout.Infinite);
        }

        public void FireAndWaitAll()
        {
            for (var i = 0; i < _timers.Count; i++)
            {
                FireTimer(i);
            }

            for (var i = 0; i < _timers.Count; i++)
            {
                WaitOnCallback(i);
            }
        }

        public void WaitOnCallback(int idx)
        {
            _timers[idx].Event.WaitOne();
        }

        public void VerifyTimer(int idx, long dueTime)
        {
            Assert.Equal(dueTime, _timers[idx].DueTime);
        }

        public Timer CreateTimer(TimerCallback callback, object state, long dueTime, long period)
        {
            Assert.Equal(Timeout.Infinite, period);

            var autoEvent = new AutoResetEvent(false);
            var timer = new Timer(s =>
            {
                callback(s);
                autoEvent.Set();
            }, state, dueTime, period);

            _timers.Add((timer, autoEvent, dueTime));

            return timer;
        }

        public Timer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            for (var i = 0; i < _timers.Count; i++)
            {
                _timers[i].Timer.Dispose();
            }
        }
    }
}

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
        private readonly List<TimerStub> _timers = new List<TimerStub>();

        public int Count => _timers.Count;

        public void FireTimer(int idx)
        {
            _timers[idx].Fire();
        }

        public void FireAll()
        {
            for (var i = 0; i < _timers.Count; i++)
            {
                FireTimer(i);
            }
        }

        public void VerifyTimer(int idx, long dueTime)
        {
            Assert.Equal(dueTime, _timers[idx].DueTime);
        }

        public void AssertTimerDisposed(int idx)
        {
            Assert.True(_timers[idx].IsDisposed);
        }

        public ITimer CreateTimer(TimerCallback callback, object state, long dueTime, long period)
        {
            Assert.Equal(Timeout.Infinite, period);
            var timer = new TimerStub(callback, state, dueTime, period);
            _timers.Add(timer);
            return timer;
        }

        public void Dispose()
        {}

        private class TimerStub : ITimer
        {
            public TimerStub(TimerCallback callback, object state, long dueTime, long period)
            {
                Callback = callback;
                State = state;
                DueTime = dueTime;
                Period = period;
            }

            public long DueTime { get; private set; }

            public long Period { get; private set; }

            public TimerCallback Callback { get; private set; }

            public object State { get; private set; }

            public bool IsDisposed { get; private set; }

            public void Change(long dueTime, long period)
            {
                DueTime = dueTime;
                Period = period;
            }

            public void Fire()
            {
                Callback(State);
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}

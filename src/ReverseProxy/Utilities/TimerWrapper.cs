// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;

namespace Yarp.ReverseProxy.Utilities
{
    internal sealed class TimerWrapper : ITimer
    {
        private readonly Timer _realTimer;

        public TimerWrapper(TimerCallback callback, object state, long dueTime, long period)
        {
            _realTimer = new Timer(callback, state, dueTime, period);
        }

        public void Change(long dueTime, long period)
        {
            _realTimer.Change(dueTime, period);
        }

        public void Dispose()
        {
            _realTimer.Dispose();
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;

namespace Microsoft.ReverseProxy.Utilities
{
    internal class TimerFactory : ITimerFactory
    {
        public Timer CreateTimer(TimerCallback callback, object state, long dueTime, long period)
        {
            return new Timer(callback, state, dueTime, period);
        }

        public Timer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            return new Timer(callback, state, dueTime, period);
        }
    }
}

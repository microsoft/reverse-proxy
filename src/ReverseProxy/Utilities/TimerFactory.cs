// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;

namespace Yarp.ReverseProxy.Utilities;

internal sealed class TimerFactory : ITimerFactory
{
    public ITimer CreateTimer(TimerCallback callback, object state, long dueTime, long period)
    {
        return new TimerWrapper(callback, state, dueTime, period);
    }
}

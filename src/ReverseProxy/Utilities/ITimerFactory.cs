// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;

namespace Microsoft.ReverseProxy.Utilities
{
    internal interface ITimerFactory
    {
        Timer CreateTimer(TimerCallback callback, object state, long dueTime, long period);
    }
}

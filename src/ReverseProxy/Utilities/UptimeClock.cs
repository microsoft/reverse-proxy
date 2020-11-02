// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;

namespace Microsoft.ReverseProxy.Utilities
{
    internal sealed class UptimeClock : IUptimeClock
    {
        public long TickCount => Environment.TickCount64;

        public long GetStopwatchTimestamp() => Stopwatch.GetTimestamp();
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Common.Tests
{
    internal class ManualClock : IClock
    {
        public TimeSpan Time { get; set; }

        public long TickCount => (long)Time.TotalMilliseconds;

        public long GetStopwatchTimestamp() => (long)(Time.TotalSeconds * Stopwatch.Frequency);
    }
}

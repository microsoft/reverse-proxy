// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Utilities
{
    internal sealed class Clock : IClock
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public long TickCount => Environment.TickCount64;

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken) =>
            Task.Delay(delay, cancellationToken);

        public Task Delay(int millisecondsDelay, CancellationToken cancellationToken) =>
            Task.Delay(millisecondsDelay, cancellationToken);

        public TimeSpan GetStopwatchTime() => _stopwatch.Elapsed;
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Utilities;

internal sealed class Clock : IClock
{
    private readonly ValueStopwatch _stopwatch = ValueStopwatch.StartNew();

    public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

    public long TickCount => Environment.TickCount64;

    public Task Delay(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);

    public Task Delay(int millisecondsDelay, CancellationToken cancellationToken) =>
        Task.Delay(millisecondsDelay, cancellationToken);

    public TimeSpan GetStopwatchTime() => _stopwatch.Elapsed;
}

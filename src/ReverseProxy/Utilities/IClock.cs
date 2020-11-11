// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Utilities
{
    /// <summary>
    /// Abstraction over monotonic time providers
    /// (Environment.TickCount64, Stopwatch.GetTimestamp, as opposed to DateTime).
    /// </summary>
    public interface IClock
    {
        long TickCount { get; }

        TimeSpan GetStopwatchTime();

        Task Delay(TimeSpan delay, CancellationToken cancellationToken);

        Task Delay(int millisecondsDelay, CancellationToken cancellationToken);
    }
}

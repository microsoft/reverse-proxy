// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Utilities
{
    /// <summary>
    /// Abstraction over time providers (Environment.TickCount64, Stopwatch.GetTimestamp)
    /// </summary>
    internal interface IClock
    {
        long TickCount { get; }

        long GetStopwatchTimestamp();
    }
}

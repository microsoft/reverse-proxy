// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions.Time;

namespace Microsoft.ReverseProxy.Utilities
{
    /// <summary>
    /// Provides a way to measure time in a monotonic fashion, immune to any system clock changes.
    /// The time is measured from the moment the class is instantiated.
    /// </summary>
    public sealed class MonotonicTimer : IMonotonicTimer
    {
        /// <summary>
        /// Specifies the minimum granularity of a scheduling tick. Larger values produce less precise scheduling. Smaller values
        /// produce unnecessary scheduling events, wasting CPU cycles and/or power.
        /// </summary>
        private static readonly TimeSpan _minimalInterval = TimeSpan.FromMilliseconds(0.1);

        /// <summary>
        /// Use a System.Diagnostics.StopWatch to measure time.
        /// </summary>
        private readonly Stopwatch _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonotonicTimer"/> class.
        /// </summary>
        public MonotonicTimer()
        {
            _timeProvider = Stopwatch.StartNew();
        }

        /// <inheritdoc />
        public TimeSpan CurrentTime => _timeProvider.Elapsed;

        /// <inheritdoc />
        public async Task DelayUntil(TimeSpan expiryTime, CancellationToken cancellationToken)
        {
            // Note: this implementation could be improved by coalescing related expirations. For example, if there's a When(12:00 noon) and When(12:30pm), then
            // the second When doesn't need to start allocating Task.Delay timers until after the first expires.
            for (; ;)
            {
                var now = CurrentTime;
                if (now >= expiryTime)
                {
                    return;
                }

                var delay = TimeUtil.Max(expiryTime - now, _minimalInterval);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}

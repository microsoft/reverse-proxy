// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.ReverseProxy.Common.Abstractions.Time;

namespace Microsoft.ReverseProxy.Common.Util
{
    /// <summary>
    /// Provides a way to measure time that tracks the wall clock time. Unlike <see cref="MonotonicTimer"/>, this class allows the possibility
    /// of time rolling backwards (e.g. clock drift corrections), so should only be used for coarse-grained time.
    /// </summary>
    public sealed class WallClockTimer : IWallClockTimer
    {
        /// <summary>
        /// Specifies the minimum granularity of a scheduling tick. Larger values produce less precise scheduling. Smaller values
        /// produce unnecessary scheduling events, wasting CPU cycles and/or power.
        /// </summary>
        private static readonly TimeSpan _minimalInterval = TimeSpan.FromMilliseconds(10);

        /// <summary>
        /// Gets the current time.
        /// </summary>
        public DateTime CurrentTime => DateTime.UtcNow;

        /// <inheritdoc />
        public async Task When(DateTime until, CancellationToken cancellationToken)
        {
            // Note: this implementation could be improved by coalesting related expirations. For example, if there's a When(12:00 noon) and When(12:30pm), then
            // the second When doesn't need to start allocating Task.Delay timers until after the first expires.
            for (; ;)
            {
                var now = CurrentTime;
                if (now >= until)
                {
                    return;
                }

                var delay = TimeUtil.Max(until - now, _minimalInterval);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Utilities
{
    /// <summary>
    /// Time and date utilities.
    /// </summary>
    public static class TimeUtil
    {
        /// <summary>
        /// This is the maximum <see cref="TimeSpan"/> that <see cref="System.Threading.CancellationTokenSource.CancelAfter(TimeSpan)"/> will accept.
        /// Use this member instead of <see cref="TimeSpan.MaxValue"/> for timeout argument validation.
        /// </summary>
        public static readonly TimeSpan MaxCancellationTokenTimeSpan = TimeSpan.FromMilliseconds(int.MaxValue);

        /// <summary>
        /// <see cref="TimeSpan"/> analog of <see cref="Math.Max(int, int)"/>; returns the more-positive of two values.
        /// </summary>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns>The more positive value of <paramref name="a"/> and <paramref name="b"/>.</returns>
        public static TimeSpan Max(TimeSpan a, TimeSpan b)
        {
            return TimeSpan.FromTicks(Math.Max(a.Ticks, b.Ticks));
        }
    }
}

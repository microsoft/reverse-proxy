// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Common.Abstractions.Time
{
    /// <summary>
    /// Abstraction for measuring time that is monotonically increasing.
    /// </summary>
    public interface IMonotonicTimer
    {
        /// <summary>
        /// Gets the current time (elapsed, relative to the creation of this timer).
        /// </summary>
        TimeSpan CurrentTime { get; }

        /// <summary>
        /// Creates a task that completes when CurrentTime >= expiryTime.
        /// </summary>
        /// <param name="expiryTime">Time at which the returned task will be completed.</param>
        /// <param name="cancelationToken">Cancelation token for the created task.</param>
        /// <returns>A task which completes at <paramref name="expiryTime"/>.</returns>
        Task DelayUntil(TimeSpan expiryTime, CancellationToken cancelationToken);
    }
}

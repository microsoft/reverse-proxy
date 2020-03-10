// <copyright file="IWallClockTimer.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;

namespace IslandGateway.Common.Abstractions.Time
{
    /// <summary>
    /// Models a way to measure time that tracks the wall clock time. Unlike IMonotonicTimer, this class allows the possibility
    /// of time rolling backwards (e.g. clock drift corrections), so should only be used for coarse-grained time.
    /// </summary>
    public interface IWallClockTimer
    {
        /// <summary>
        /// Gets the current time.
        /// </summary>
        DateTime CurrentTime { get; }

        /// <summary>
        /// Produces a task that completes when the current time equals or exceeds the desired time.
        /// </summary>
        /// <param name="until">Time to wait until.</param>
        /// <param name="cancellationToken">Cancelation token to cancel the request.</param>
        /// <returns>A task that completes when CurrentTime first equals or exceeds the desired time.</returns>
        Task When(DateTime until, CancellationToken cancellationToken);
    }
}

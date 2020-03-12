// <copyright file="IRecurrentActivityManager.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Common.Abstractions.Telemetry;

namespace IslandGateway.Utilities
{
    /// <summary>
    /// Assists in regularly executing a set of simple background tasks.
    /// </summary>
    /// <remarks>
    /// This class is optimized for very simple background tasks. Different activities can be executed at the same time, but only
    /// one instance of a given activity is running at any point. If an activity is still running when its next scheduling comes up,
    /// the next scheduling is skipped -- skipping will continue until the previous one completes.
    /// </remarks>
    public interface IRecurrentActivityManager
    {
        /// <summary>
        /// Scheduling granularity configures the minimum sleep interval between scheduling operations. By scheduling groups
        /// of operations together, we reduce the frequency of thread scheduling to reduce compute load on the machine.
        /// </summary>
        TimeSpan SchedulingGranularity { get; set; }

        /// <summary>
        /// Starts scheduling of registered background tasks.
        /// </summary>
        void Start();

        /// <summary>
        /// Terminates background scheduling.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Add an item of work to be executed with a certain interval.
        /// </summary>
        /// <param name="operationName">Name of the operation used with <see cref="IOperationLogger"/> on each execution of this piece of work.</param>
        /// <param name="schedulingInterval">Interval with which tasks should be scheduled.</param>
        /// <param name="action">Instance of <see cref="Action"/> to be executed.</param>
        /// <param name="executeImmediately">Boolean indicating whether the <paramref name="action"/> should be executed immediately.</param>
        void AddWork(string operationName, TimeSpan schedulingInterval, Action action, bool executeImmediately = false);

        /// <summary>
        /// Add an item of work to be executed with a certain interval.
        /// </summary>
        /// <param name="operationName">Name of the operation used with <see cref="IOperationLogger"/> on each execution of this piece of work.</param>
        /// <param name="schedulingInterval">Interval with which tasks should be scheduled.</param>
        /// <param name="func">Instance of <see cref="Func{Task}"/> to be executed.</param>
        /// <param name="executeImmediately">Boolean indicating whether the <paramref name="func"/> should be executed immediately.</param>
        void AddWork(string operationName, TimeSpan schedulingInterval, Func<Task> func, bool executeImmediately = false);

        /// <summary>
        /// Add an item of work to be executed with a certain interval.
        /// </summary>
        /// <param name="operationName">Name of the operation used with <see cref="IOperationLogger"/> on each execution of this piece of work.</param>
        /// <param name="schedulingInterval">Interval with which tasks should be scheduled.</param>
        /// <param name="func">Instance of <see cref="Func{CancellationToken, Task}"/> to be executed.</param>
        /// <param name="executeImmediately">Boolean indicating whether the <paramref name="func"/> should be executed immediately.</param>
        void AddWork(string operationName, TimeSpan schedulingInterval, Func<CancellationToken, Task> func, bool executeImmediately = false);
    }
}

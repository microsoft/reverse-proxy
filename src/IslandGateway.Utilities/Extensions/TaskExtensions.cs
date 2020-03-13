// ------------------------------------------------------------------------------
// <copyright file="TaskExtensions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace IslandGateway.Utilities
{
    /// <summary>
    /// Extensions for the <see cref="Task"/> class.
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Runs an action on the current scheduler instead of the default scheduler.
        /// </summary>
        /// <param name="scheduler">Scheduler for the action to be scheduled on.</param>
        /// <param name="action">Action to be scheduled.</param>
        /// <param name="cancelationToken">Cancelation token to link the new task to. If canceled before being scheduled, the action will not be run.</param>
        /// <returns>New task created for the action.</returns>
        public static Task Run(this TaskScheduler scheduler, Action action, CancellationToken cancelationToken = default(CancellationToken))
        {
            var taskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskContinuationOptions.None, scheduler);
            return taskFactory.StartNew(action, cancellationToken: cancelationToken);
        }

        /// <summary>
        /// Runs a function on the current scheduler instead of the default scheduler.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="scheduler">Scheduler for the action to be scheduled on.</param>
        /// <param name="function">Function to be scheduled.</param>
        /// <param name="cancelationToken">Cancelation token to link the new task to. If canceled before being scheduled, the action will not be run.</param>
        /// <returns>New task created for the function. This task completes with the result of calling the function.</returns>
        public static Task<T> Run<T>(this TaskScheduler scheduler, Func<T> function, CancellationToken cancelationToken = default(CancellationToken))
        {
            var taskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskContinuationOptions.None, scheduler);
            return taskFactory.StartNew(function, cancellationToken: cancelationToken);
        }

        /// <summary>
        /// Runs a function on the current scheduler instead of the default scheduler.
        /// </summary>
        /// <param name="scheduler">Scheduler for the action to be scheduled on.</param>
        /// <param name="function">Function to be scheduled.</param>
        /// <param name="cancelationToken">Cancelation token to link the new task to. If canceled before being scheduled, the action will not be run.</param>
        /// <returns>New task created for the function. This task completes with the result of the task returned by the function.</returns>
        public static async Task Run(this TaskScheduler scheduler, Func<Task> function, CancellationToken cancelationToken = default(CancellationToken))
        {
            var taskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskContinuationOptions.None, scheduler);
            var innerTask = await taskFactory.StartNew(function, cancellationToken: cancelationToken);
            await innerTask;
        }

        /// <summary>
        /// Runs a function on the current scheduler instead of the default scheduler.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="scheduler">Scheduler for the action to be scheduled on.</param>
        /// <param name="function">Function to be scheduled.</param>
        /// <param name="cancelationToken">Cancelation token to link the new task to. If canceled before being scheduled, the action will not be run.</param>
        /// <returns>New task created for the function. This task completes with the result of the task returned by the function.</returns>
        public static async Task<T> Run<T>(this TaskScheduler scheduler, Func<Task<T>> function, CancellationToken cancelationToken = default(CancellationToken))
        {
            var taskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskContinuationOptions.None, scheduler);
            var innerTask = await taskFactory.StartNew(function, cancellationToken: cancelationToken);
            return await innerTask;
        }

        /// <summary>
        /// Returns a <see cref="SwitchSchedulerAwaiter"/>, which runs the continuation on the specified scheduler.
        /// </summary>
        /// <param name="scheduler">Scheduler to resume execution on.</param>
        public static SwitchSchedulerAwaiter SwitchTo(this TaskScheduler scheduler)
        {
            return new SwitchSchedulerAwaiter(scheduler);
        }

        /// <summary>
        /// Custom awaiter that resumes the continuation on the specified scheduler.
        /// </summary>
        public struct SwitchSchedulerAwaiter : INotifyCompletion
        {
            private readonly TaskScheduler _scheduler;

            /// <summary>
            /// Initializes a new instance of the <see cref="SwitchSchedulerAwaiter"/> struct.
            /// </summary>
            public SwitchSchedulerAwaiter(TaskScheduler scheduler)
            {
                this._scheduler = scheduler;
            }

            /// <summary>
            /// Whether the switch is completed.
            /// </summary>
            public bool IsCompleted => this._scheduler == TaskScheduler.Current;

            /// <summary>
            /// Part of custom awaiter pattern.
            /// </summary>
            public void GetResult()
            {
            }

            /// <summary>
            /// Part of custom awaiter pattern.
            /// </summary>
            public SwitchSchedulerAwaiter GetAwaiter() => this;

            /// <inheritdoc/>
            public void OnCompleted(Action continuation)
            {
                this._scheduler.Run(continuation);
            }
        }
    }
}

// ------------------------------------------------------------------------------
// <copyright file="TaskExtensions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Common.Tests
{
    /// <summary>
    /// Extensions for the <see cref="Task"/> class.
    /// </summary>
    internal static class TaskExtensions
    {
        public static TimeSpan DefaultTimeoutTimeSpan { get; } = TimeSpan.FromSeconds(5);

        public static Task<T> DefaultTimeout<T>(this ValueTask<T> task)
        {
            return task.AsTask().TimeoutAfter(DefaultTimeoutTimeSpan);
        }

        public static Task DefaultTimeout(this ValueTask task)
        {
            return task.AsTask().TimeoutAfter(DefaultTimeoutTimeSpan);
        }

        public static Task<T> DefaultTimeout<T>(this Task<T> task)
        {
            return task.TimeoutAfter(DefaultTimeoutTimeSpan);
        }

        public static Task DefaultTimeout(this Task task)
        {
            return task.TimeoutAfter(DefaultTimeoutTimeSpan);
        }

        private static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = default)
        {
            // Don't create a timer if the task is already completed
            // or the debugger is attached
            if (task.IsCompleted || Debugger.IsAttached)
            {
                return await task;
            }

            var cts = new CancellationTokenSource();
            if (task == await Task.WhenAny(task, Task.Delay(timeout, cts.Token)))
            {
                cts.Cancel();
                return await task;
            }
            else
            {
                throw new TimeoutException(CreateMessage(timeout, filePath, lineNumber));
            }
        }

        private static async Task TimeoutAfter(this Task task, TimeSpan timeout,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int lineNumber = default)
        {
            // Don't create a timer if the task is already completed
            // or the debugger is attached
            if (task.IsCompleted || Debugger.IsAttached)
            {
                await task;
                return;
            }

            var cts = new CancellationTokenSource();
            if (task == await Task.WhenAny(task, Task.Delay(timeout, cts.Token)))
            {
                cts.Cancel();
                await task;
            }
            else
            {
                throw new TimeoutException(CreateMessage(timeout, filePath, lineNumber));
            }
        }

        private static string CreateMessage(TimeSpan timeout, string filePath, int lineNumber)
            => string.IsNullOrEmpty(filePath)
            ? $"The operation timed out after reaching the limit of {timeout.TotalMilliseconds}ms."
            : $"The operation at {filePath}:{lineNumber} timed out after reaching the limit of {timeout.TotalMilliseconds}ms.";

        /// <summary>
        /// Runs an action on the current scheduler instead of the default scheduler.
        /// </summary>
        /// <param name="scheduler">Scheduler for the action to be scheduled on.</param>
        /// <param name="action">Action to be scheduled.</param>
        /// <param name="cancelationToken">Cancelation token to link the new task to. If canceled before being scheduled, the action will not be run.</param>
        /// <returns>New task created for the action.</returns>
        public static Task Run(this TaskScheduler scheduler, Action action, CancellationToken cancelationToken = default)
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
        public static Task<T> Run<T>(this TaskScheduler scheduler, Func<T> function, CancellationToken cancelationToken = default)
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
        public static async Task Run(this TaskScheduler scheduler, Func<Task> function, CancellationToken cancelationToken = default)
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
        public static async Task<T> Run<T>(this TaskScheduler scheduler, Func<Task<T>> function, CancellationToken cancelationToken = default)
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
                _scheduler = scheduler;
            }

            /// <summary>
            /// Whether the switch is completed.
            /// </summary>
            public bool IsCompleted => _scheduler == TaskScheduler.Current;

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
                _scheduler.Run(continuation);
            }
        }
    }
}

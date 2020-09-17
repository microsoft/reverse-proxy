// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Common.Tests
{
    /// <summary>
    /// Task scheduler that only schedules execution of one task at a time.
    /// </summary>
    /// <remarks>
    /// This task scheduler included the ability to wait for all scheduled tasks to complete. This is intended for testing
    /// where repeatability is paramount. Use with the <see cref="VirtualMonotonicTimer"/> in testing time-related policies
    /// for repeatable time related tests that don't have to wait for wall clock time to elapse (e.g. simulating minutes or hours
    /// in milliseconds of real time; testing of timeout and rate limiting policies).
    /// <para/>
    /// Note that while this task scheduler only runs one task at a time, this doesn't prevent async methods from running
    /// concurrently. Async methods allocate one task between every pair of blocking awaits. If two async methods are running, they
    /// end up taking turns, where one is running until an await blocks the async method, allowing the second one a turn to run until
    /// it blocks, back and forth.
    /// </remarks>
    public class SingleThreadedTaskScheduler : TaskScheduler
    {
        private readonly object _lockObject = new object();
        private readonly Queue<Task> _taskQueue = new Queue<Task>();
        private bool _schedulerIsRunning = false;

        // An indication to the scheduler to either attempt or not attempt to execute any scheduled tasks upon scheduling.
        // If the value is true, a call to TaskScheduler.Run() will queue the task on the current scheduler and attempt to execute
        // the tasks on the queue in the order they were put on the queue.
        // If the value is false, a call to TaskScheduler.Run() will queue the task on the current scheduler, but not execute it.
        private bool _suspendScheduler = false;

        /// <summary>
        /// Gets or sets callback to invoke whenever the scheduler goes idle. This gives listeners a chance to create more work to schedule before the scheduler loop terminates.
        /// </summary>
        public Action OnIdle { get; set; }

        /// <summary>
        /// Helper function to run an async method until completion.
        /// This should be used in cases when you have exercised the main action and don't care about dangling work on the scheduler.
        /// </summary>
        public void RunUntilComplete(Func<Task> func)
        {
            lock (_lockObject)
            {
                if (_schedulerIsRunning)
                {
                    throw new ArgumentException("Synchronous execution is not supported if already being executed.");
                }

                var flag = false;
                _suspendScheduler = true;

                try
                {
                    var ignore = this.Run(async () => { await func(); flag = true; });
                }
                finally
                {
                    _suspendScheduler = false;
                }

                ExecuteTasksUntil(() => flag);

                if (!flag)
                {
                    throw new ArgumentException("Task did not execute synchronously. Check for any non-single threaded work.");
                }
            }
        }

        /// <inheritdoc/>
        protected override void QueueTask(Task task)
        {
            lock (_lockObject)
            {
                _taskQueue.Enqueue(task);

                ExecuteTasksUntil(() => false);
            }
        }

        /// <inheritdoc/>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        /// <inheritdoc/>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _taskQueue;
        }

        /// <summary>
        /// Runs tasks in single threaded fashion until queue is emptied or the condition of the predicate is false.
        /// </summary>
        /// <remarks>
        /// It's critical that this method is not invoked with itself as the current scheduler. It must run on another scheduler (such
        /// as the default scheduler), or a deadlock will occur.
        /// </remarks>
        private void ExecuteTasksUntil(Func<bool> predicate)
        {
            if (!Monitor.IsEntered(_lockObject))
            {
                throw new ArgumentException("Must hold lock when scheduling.");
            }

            // Prevent reentrancy. Reentrancy can lead to stack overflow and difficulty when debugging.
            if (_schedulerIsRunning || _suspendScheduler)
            {
                return;
            }

            try
            {
                _schedulerIsRunning = true;
                while (predicate() == false)
                {
                    if (_taskQueue.Count == 0)
                    {
                        if (OnIdle != null)
                        {
                            OnIdle();

                            // OnIdle handler may have created more tasks. Try finding more work to execute.
                            if (_taskQueue.Count != 0)
                            {
                                continue;
                            }
                        }

                        // No remaining work to execute. Return.
                        return;
                    }

                    var nextTask = _taskQueue.Dequeue();
                    TryExecuteTask(nextTask);
                }
            }
            finally
            {
                _schedulerIsRunning = false;
            }
        }
    }
}

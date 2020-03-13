// <copyright file="RecurrentActivityManager.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Common.Abstractions.Telemetry;
using IslandGateway.Common.Abstractions.Time;

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
    // TODO: 6106582: Find suitable replacement for RecurrentActivityManager
    public class RecurrentActivityManager : IRecurrentActivityManager
    {
        private readonly object _monitor = new object();
        private readonly IOperationLogger _operationLogger;
        private readonly IMonotonicTimer _monotonicTimer;
        private readonly List<WorkItem> _workItemList = new List<WorkItem>();

        // Start / Stop fields.
        private Task _backgroundPollingLoopTask;
        private CancellationTokenSource _backgroundPollingLoopCancellation;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecurrentActivityManager"/> class.
        /// </summary>
        /// <param name="operationLogger">The operation logger.</param>
        /// <param name="monotonicTimer">Instance of <see cref="IMonotonicTimer"/> to aid in testability of the class.</param>
        public RecurrentActivityManager(
            IOperationLogger operationLogger,
            IMonotonicTimer monotonicTimer)
        {
            Contracts.CheckValue(operationLogger, nameof(operationLogger));
            Contracts.CheckValue(monotonicTimer, nameof(monotonicTimer));

            this._operationLogger = operationLogger;
            this._monotonicTimer = monotonicTimer;
        }

        /// <inheritdoc />
        public TimeSpan SchedulingGranularity { get; set; } = TimeSpan.FromMinutes(1);

        /// <inheritdoc />
        public void Start()
        {
            Contracts.Check(this._backgroundPollingLoopTask == null, "StartPolling must not be called when already started.");

            // We don't expect the previous cancellation token to be still in use, but it would be preferable to not leak it.
            this._backgroundPollingLoopCancellation?.Dispose();

            var cancellationTokenSource = new CancellationTokenSource();
            this._backgroundPollingLoopCancellation = cancellationTokenSource;
            this._backgroundPollingLoopTask = TaskScheduler.Current.Run(() => this.StartImpl(cancellationTokenSource.Token));
        }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            Contracts.Check(this._backgroundPollingLoopTask != null, "Start must be called before Stop is called.");
            try
            {
                this._backgroundPollingLoopCancellation.Cancel();
                await this._backgroundPollingLoopTask;
            }
            finally
            {
                this._backgroundPollingLoopCancellation.Dispose();
                this._backgroundPollingLoopCancellation = null;
                this._backgroundPollingLoopTask = null;
            }
        }

        /// <inheritdoc />
        public void AddWork(string operationName, TimeSpan schedulingInterval, Action action, bool executeImmediately = false)
        {
            Task FuncWrapper(CancellationToken cancellationToken)
            {
                action();
                return Task.CompletedTask;
            };
            this.AddWork(operationName, schedulingInterval, FuncWrapper, executeImmediately);
        }

        /// <inheritdoc />
        public void AddWork(string operationName, TimeSpan schedulingInterval, Func<Task> func, bool executeImmediately = false)
        {
            this.AddWork(operationName, schedulingInterval, cancellation => func(), executeImmediately);
        }

        /// <inheritdoc />
        public void AddWork(string operationName, TimeSpan schedulingInterval, Func<CancellationToken, Task> func, bool executeImmediately = false)
        {
            var workItem = new WorkItem
            {
                OperationName = operationName,
                SchedulingInterval = schedulingInterval,
                Func = func,
                LastExecution = this._monotonicTimer.CurrentTime,
            };
            if (executeImmediately)
            {
                workItem.Start(this._operationLogger, this._monotonicTimer);
            }
            lock (this._monitor)
            {
                this._workItemList.Add(workItem);
            }
        }

        /// <summary>
        /// Background execution loop.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task tracking execution.</returns>
        private async Task StartImpl(CancellationToken cancellationToken)
        {
            try
            {
                for (; ;)
                {
                    // Note: this approach to scheduling assumes that all tasks are added
                    // before Start is called, otherwise tasks added after Start is called might
                    // not get their first run on time, running when the next pre-existing task
                    // was scheduled.
                    var wakeTime = this.GetNextWakeTime();
                    await this._monotonicTimer.DelayUntil(wakeTime, cancellationToken);

                    this.ExecuteEligibleWork(wakeTime);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Executes all of the work items that are eligible to be scheduled.
        /// </summary>
        /// <param name="wakeTime">The time when execution was resumed.</param>
        private void ExecuteEligibleWork(TimeSpan wakeTime)
        {
            this._operationLogger.Execute(
                "CoreServices.Common.ExecuteEligibleWork",
                () =>
                {
                    List<WorkItem> tasksToRun = null;
                    lock (this._monitor)
                    {
                        foreach (var item in this._workItemList)
                        {
                            if (item.CanRun(wakeTime))
                            {
                                (tasksToRun ?? (tasksToRun = new List<WorkItem>())).Add(item);
                            }
                        }
                    }
                    if (tasksToRun != null)
                    {
                        foreach (var item in tasksToRun)
                        {
                            item.Start(this._operationLogger, this._monotonicTimer);
                        }
                    }
                });
        }

        /// <summary>
        /// Gets the <see cref="DateTime"/> when the task should resume execution based on
        /// the scheduling granularity of each work item.
        /// </summary>
        /// <returns>Next execution time.</returns>
        private TimeSpan GetNextWakeTime()
        {
            var currentTime = this._monotonicTimer.CurrentTime;
            lock (this._monitor)
            {
                if (this._workItemList.Count == 0)
                {
                    return this._monotonicTimer.CurrentTime + this.SchedulingGranularity;
                }

                return this._workItemList.Min(item => item.NextExecution(currentTime));
            }
        }

        /// <summary>
        /// Class that is used to represent a unit of work.
        /// </summary>
        private class WorkItem
        {
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private volatile bool _isExecuting;

            public TimeSpan LastExecution { get; set; }
            public TimeSpan SchedulingInterval { get; set; }

            public string OperationName { get; set; }
            public Func<CancellationToken, Task> Func { get; set; }

            /// <summary>
            /// Determines whether the work item can run given the current time.
            /// </summary>
            /// <param name="currentTime">Instance of <see cref="DateTime"/> representing current clock time.</param>
            /// <returns>Boolean indicating whether the task can run.</returns>
            internal bool CanRun(TimeSpan currentTime)
            {
                return !this._isExecuting && currentTime >= this.LastExecution + this.SchedulingInterval;
            }

            /// <summary>
            /// Determines the next execution based on the current time.
            /// </summary>
            /// <param name="currentTime">The current clock time.</param>
            /// <returns>Next execution time.</returns>
            /// <remarks>The next execution time has to be based on the current execution time rather than last
            /// execution time of the action because otherwise long-running actions will incorrectly report the amount of time
            /// the executor task should delay its execution for.</remarks>
            internal TimeSpan NextExecution(TimeSpan currentTime)
            {
                return currentTime + this.SchedulingInterval;
            }

            /// <summary>
            /// Start the execution of a work item.
            /// </summary>
            /// <param name="operationLogger">Instance of <see cref="IOperationLogger"/>.</param>
            /// <param name="monotonicTimer">Instance of <see cref="IMonotonicTimer"/>.</param>
            internal async void Start(IOperationLogger operationLogger, IMonotonicTimer monotonicTimer)
            {
                Contracts.Check(this._isExecuting == false, "Expected work item was not executing");

                this._isExecuting = true;
                this.LastExecution = monotonicTimer.CurrentTime;
                try
                {
                    await operationLogger.ExecuteAsync(
                        this.OperationName,
                        async () =>
                        {
                            await this.Func(this._cts.Token);
                        });
                }
                catch (Exception e) when (!e.IsFatal())
                {
                    // Exceptions are not expected, but are already logged above and service must remain executing.
                }
                finally
                {
                    this._isExecuting = false;
                }
            }
        }
    }
}

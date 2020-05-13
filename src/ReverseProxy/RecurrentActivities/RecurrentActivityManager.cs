// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Abstractions.Time;

namespace Microsoft.ReverseProxy.Utilities
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
        private readonly IOperationLogger<RecurrentActivityManager> _operationLogger;
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
            IOperationLogger<RecurrentActivityManager> operationLogger,
            IMonotonicTimer monotonicTimer)
        {
            Contracts.CheckValue(operationLogger, nameof(operationLogger));
            Contracts.CheckValue(monotonicTimer, nameof(monotonicTimer));

            _operationLogger = operationLogger;
            _monotonicTimer = monotonicTimer;
        }

        /// <inheritdoc />
        public TimeSpan SchedulingGranularity { get; set; } = TimeSpan.FromMinutes(1);

        /// <inheritdoc />
        public void Start()
        {
            Contracts.Check(_backgroundPollingLoopTask == null, "StartPolling must not be called when already started.");

            // We don't expect the previous cancellation token to be still in use, but it would be preferable to not leak it.
            _backgroundPollingLoopCancellation?.Dispose();

            var cancellationTokenSource = new CancellationTokenSource();
            _backgroundPollingLoopCancellation = cancellationTokenSource;
            _backgroundPollingLoopTask = TaskScheduler.Current.Run(() => StartImpl(cancellationTokenSource.Token));
        }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            Contracts.Check(_backgroundPollingLoopTask != null, "Start must be called before Stop is called.");
            try
            {
                _backgroundPollingLoopCancellation.Cancel();
                await _backgroundPollingLoopTask;
            }
            finally
            {
                _backgroundPollingLoopCancellation.Dispose();
                _backgroundPollingLoopCancellation = null;
                _backgroundPollingLoopTask = null;
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
            AddWork(operationName, schedulingInterval, FuncWrapper, executeImmediately);
        }

        /// <inheritdoc />
        public void AddWork(string operationName, TimeSpan schedulingInterval, Func<Task> func, bool executeImmediately = false)
        {
            AddWork(operationName, schedulingInterval, cancellation => func(), executeImmediately);
        }

        /// <inheritdoc />
        public void AddWork(string operationName, TimeSpan schedulingInterval, Func<CancellationToken, Task> func, bool executeImmediately = false)
        {
            var workItem = new WorkItem
            {
                OperationName = operationName,
                SchedulingInterval = schedulingInterval,
                Func = func,
                LastExecution = _monotonicTimer.CurrentTime,
            };
            if (executeImmediately)
            {
                workItem.Start(_operationLogger, _monotonicTimer);
            }
            lock (_monitor)
            {
                _workItemList.Add(workItem);
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
                for (; ; )
                {
                    // Note: this approach to scheduling assumes that all tasks are added
                    // before Start is called, otherwise tasks added after Start is called might
                    // not get their first run on time, running when the next pre-existing task
                    // was scheduled.
                    var wakeTime = GetNextWakeTime();
                    await _monotonicTimer.DelayUntil(wakeTime, cancellationToken);

                    ExecuteEligibleWork(wakeTime);
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
            _operationLogger.Execute(
                "CoreServices.Common.ExecuteEligibleWork",
                () =>
                {
                    List<WorkItem> tasksToRun = null;
                    lock (_monitor)
                    {
                        foreach (var item in _workItemList)
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
                            item.Start(_operationLogger, _monotonicTimer);
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
            var currentTime = _monotonicTimer.CurrentTime;
            lock (_monitor)
            {
                if (_workItemList.Count == 0)
                {
                    return _monotonicTimer.CurrentTime + SchedulingGranularity;
                }

                return _workItemList.Min(item => item.NextExecution(currentTime));
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
                return !_isExecuting && currentTime >= LastExecution + SchedulingInterval;
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
                return currentTime + SchedulingInterval;
            }

            /// <summary>
            /// Start the execution of a work item.
            /// </summary>
            /// <param name="operationLogger">Instance of <see cref="IOperationLogger"/>.</param>
            /// <param name="monotonicTimer">Instance of <see cref="IMonotonicTimer"/>.</param>
            internal async void Start(IOperationLogger<RecurrentActivityManager> operationLogger, IMonotonicTimer monotonicTimer)
            {
                Contracts.Check(_isExecuting == false, "Expected work item was not executing");

                _isExecuting = true;
                LastExecution = monotonicTimer.CurrentTime;
                try
                {
                    await operationLogger.ExecuteAsync(
                        OperationName,
                        async () =>
                        {
                            await Func(_cts.Token);
                        });
                }
                catch (Exception e) when (!e.IsFatal())
                {
                    // Exceptions are not expected, but are already logged above and service must remain executing.
                }
                finally
                {
                    _isExecuting = false;
                }
            }
        }
    }
}

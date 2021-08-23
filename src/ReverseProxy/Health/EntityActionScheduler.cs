// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Health
{
    /// <summary>
    /// Periodically invokes specified actions on registered entities.
    /// </summary>
    /// <remarks>
    /// It creates a separate <see cref="Timer"/> for each registration which is considered
    /// reasonably efficient because .NET already maintains a process-wide managed timer queue.
    /// There are 2 scheduling modes supported: run once and infinite run. In "run once" mode,
    /// an entity gets unscheduled after the respective timer fired for the first time whereas
    /// in "infinite run" entities get repeatedly rescheduled until either they are explicitly removed
    /// or the <see cref="EntityActionScheduler{T}"/> instance is disposed.
    /// </remarks>
    internal sealed class EntityActionScheduler<T> : IDisposable where T : notnull
    {
        private readonly ConcurrentDictionary<T, SchedulerEntry> _entries = new();
        private readonly WeakReference<EntityActionScheduler<T>> _weakThisRef;
        private readonly Func<T, Task> _action;
        private readonly bool _runOnce;
        private readonly ITimerFactory _timerFactory;

        private const int Status_NotStarted = 0;
        private const int Status_Started = 1;
        private const int Status_Disposed = 2;
        private int _status;

        public EntityActionScheduler(Func<T, Task> action, bool autoStart, bool runOnce, ITimerFactory timerFactory)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _runOnce = runOnce;
            _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            _status = autoStart ? Status_Started : Status_NotStarted;
            _weakThisRef = new WeakReference<EntityActionScheduler<T>>(this);
        }

        public void Dispose()
        {
            Volatile.Write(ref _status, Status_Disposed);

            foreach(var entry in _entries.Values)
            {
                entry.Dispose();
            }
        }

        public void Start()
        {
            if (Interlocked.CompareExchange(ref _status, Status_Started, Status_NotStarted) != Status_NotStarted)
            {
                return;
            }

            foreach (var entry in _entries.Values)
            {
                entry.EnsureStarted();
            }
        }

        public void ScheduleEntity(T entity, TimeSpan period)
        {
            // Ensure the Timer has a weak reference to this scheduler; otherwise,
            // EntityActionScheduler can be rooted by the Timer implementation.
            var entry = new SchedulerEntry(_weakThisRef, entity, (long)period.TotalMilliseconds, _timerFactory);

            if (_entries.TryAdd(entity, entry))
            {
                // Scheduler could have been started while we were adding the new entry.
                // Start timer here to ensure it's not forgotten.
                if (Volatile.Read(ref _status) == Status_Started)
                {
                    entry.EnsureStarted();
                }
            }
            else
            {
                entry.Dispose();
            }
        }

        public void ChangePeriod(T entity, TimeSpan newPeriod)
        {
            Debug.Assert(!_runOnce, "Calling ChangePeriod on a RunOnce scheduler may cause the callback to fire twice");

            if (_entries.TryGetValue(entity, out var entry))
            {
                entry.ChangePeriod((long)newPeriod.TotalMilliseconds);
                if (Volatile.Read(ref _status) == Status_Started)
                {
                    entry.EnsureStarted();
                }
            }
            else
            {
                ScheduleEntity(entity, newPeriod);
            }
        }

        public void UnscheduleEntity(T entity)
        {
            if (_entries.TryRemove(entity, out var entry))
            {
                entry.Dispose();
            }
        }

        public bool IsScheduled(T entity)
        {
            return _entries.ContainsKey(entity);
        }

        private sealed class SchedulerEntry : IDisposable
        {
            private static readonly TimerCallback _timerCallback = static async state =>
            {
                var entry = (SchedulerEntry)state!;

                if (!entry._scheduler.TryGetTarget(out var scheduler))
                {
                    return;
                }

                var pair = new KeyValuePair<T, SchedulerEntry>(entry._entity, entry);

                if (scheduler._runOnce && scheduler._entries.TryRemove(pair))
                {
                    entry.Dispose();
                }

                try
                {
                    await scheduler._action(entry._entity);

                    if (!scheduler._runOnce && scheduler._entries.Contains(pair))
                    {
                        // This entry has not been unscheduled - set the timer again
                        entry.SetTimer(isRestart: true);
                    }
                }
                catch (Exception ex)
                {
                    // We are running on the ThreadPool, don't propagate excetions
                    Debug.Fail(ex.ToString()); // TODO: Log
                    if (scheduler._entries.TryRemove(pair))
                    {
                        entry.Dispose();
                    }
                    return;
                }
            };

            private readonly WeakReference<EntityActionScheduler<T>> _scheduler;
            private readonly T _entity;
            private readonly ITimer _timer;
            private long _period;
            private bool _running;

            public SchedulerEntry(WeakReference<EntityActionScheduler<T>> scheduler, T entity, long period, ITimerFactory timerFactory)
            {
                _scheduler = scheduler;
                _entity = entity;
                _period = period;

                // Don't capture the current ExecutionContext and its AsyncLocals onto the timer causing them to live forever
                var restoreFlow = false;
                try
                {
                    if (!ExecutionContext.IsFlowSuppressed())
                    {
                        ExecutionContext.SuppressFlow();
                        restoreFlow = true;
                    }

                    _timer = timerFactory.CreateTimer(_timerCallback, this, Timeout.Infinite, Timeout.Infinite);
                }
                finally
                {
                    if (restoreFlow)
                    {
                        ExecutionContext.RestoreFlow();
                    }
                }
            }

            public void ChangePeriod(long newPeriod) => Volatile.Write(ref _period, newPeriod);

            public void EnsureStarted() => SetTimer(isRestart: false);

            private void SetTimer(bool isRestart)
            {
                long period;

                lock (this)
                {
                    period = Volatile.Read(ref _period);

                    if (isRestart)
                    {
                        Debug.Assert(_running);
                        _running = false;
                    }

                    if (period == Timeout.Infinite || _running)
                    {
                        return;
                    }

                    _running = true;
                }

                try
                {
                    _timer.Change(period, Timeout.Infinite);
                }
                catch (ObjectDisposedException)
                {
                    // It can be thrown if the timer has been already disposed.
                    // Just suppress it.
                }
            }

            public void Dispose()
            {
                _timer.Dispose();
            }
        }
    }
}

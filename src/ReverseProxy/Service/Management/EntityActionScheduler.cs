// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.ReverseProxy.Service.Management
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
    internal class EntityActionScheduler<T> : IDisposable
    {
        private readonly ConcurrentDictionary<T, SchedulerEntry> _entries = new ConcurrentDictionary<T, SchedulerEntry>();
        private readonly Action<T> _action;
        private readonly bool _runOnce;
        private int _isStarted;

        public EntityActionScheduler(Action<T> action, bool autoStart, bool runOnce)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _runOnce = runOnce;
            _isStarted = autoStart ? 1 : 0;
        }

        public IEnumerable<T> GetScheduledEntities()
        {
            return _entries.Keys;
        }

        public void Dispose()
        {
            foreach(var entry in _entries.Values)
            {
                entry.Timer.Dispose();
            }
        }

        public void Start()
        {
            if (Interlocked.CompareExchange(ref _isStarted, 1, 0) != 0)
            {
                return;
            }

            foreach (var entry in _entries.Values)
            {
                entry.Timer.Change(entry.Period, Timeout.Infinite);
            }
        }

        public void ScheduleEntity(T entity, TimeSpan period)
        {
            var entry = new SchedulerEntry(entity, (long)period.TotalMilliseconds, Run, Volatile.Read(ref _isStarted) == 1);
            _entries.TryAdd(entity, entry);
        }

        public void ChangePeriod(T entity, TimeSpan newPeriod)
        {
            if (_entries.TryGetValue(entity, out var entry))
            {
                entry.ChangePeriod((long)newPeriod.TotalMilliseconds);
            }
        }

        public void UnscheduleEntity(T entity)
        {
            if (_entries.TryRemove(entity, out var entry))
            {
                entry.Timer.Dispose();
            }
        }

        private void Run(object entryObj)
        {
            lock (entryObj)
            {
                var entry = (SchedulerEntry)entryObj;

                if (_runOnce)
                {
                    UnscheduleEntity(entry.Entity);
                }

                _action(entry.Entity);

                // Check if the entity is still scheduled.
                if (_entries.ContainsKey(entry.Entity))
                {
                    entry.SetTimer();
                }
            }
        }

        private void ChangePeriodInternal()
        {}

        private class SchedulerEntry
        {
            private long _period;

            public SchedulerEntry(T entity, long period, TimerCallback timerCallback, bool autoStart)
            {
                Entity = entity;
                _period = period;
                Timer = new Timer(timerCallback, this, autoStart ? period : Timeout.Infinite, Timeout.Infinite);
            }

            public T Entity { get; }

            public long Period => _period;

            public Timer Timer { get; }

            public void ChangePeriod(long newPeriod)
            {
                Interlocked.Exchange(ref _period, newPeriod);
                SetTimer();
            }

            public void SetTimer()
            {
                try
                {
                    Timer.Change(Period, Timeout.Infinite);
                }
                catch (ObjectDisposedException)
                {
                    // It can be thrown if the timer has been already disposed.
                    // Just suppress it.
                }
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Service.Management
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
    internal sealed class EntityActionScheduler<T> : IDisposable
    {
        private readonly ConcurrentDictionary<T, SchedulerEntry> _entries = new ConcurrentDictionary<T, SchedulerEntry>();
        private readonly Func<T, Task> _action;
        private readonly bool _runOnce;
        private readonly ITimerFactory _timerFactory;
        private readonly TimerCallback _timerCallback;
        private int _isStarted;

        public EntityActionScheduler(Func<T, Task> action, bool autoStart, bool runOnce, ITimerFactory timerFactory)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _runOnce = runOnce;
            _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            _timerCallback = async o => await Run(o);
            _isStarted = autoStart ? 1 : 0;
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
            var entry = new SchedulerEntry(entity, (long)period.TotalMilliseconds, _timerCallback, Volatile.Read(ref _isStarted) == 1, _timerFactory);
            var added = _entries.TryAdd(entity, entry);
            Debug.Assert(added);
        }

        public void ChangePeriod(T entity, TimeSpan newPeriod)
        {
            if (_entries.TryGetValue(entity, out var entry))
            {
                entry.ChangePeriod((long)newPeriod.TotalMilliseconds, Volatile.Read(ref _isStarted) == 1);
            }
        }

        public void UnscheduleEntity(T entity)
        {
            if (_entries.TryRemove(entity, out var entry))
            {
                entry.Timer.Dispose();
            }
        }

        public bool IsScheduled(T entity)
        {
            return _entries.ContainsKey(entity);
        }

        private async Task Run(object entryObj)
        {
            var entry = (SchedulerEntry)entryObj;

            if (_runOnce)
            {
                UnscheduleEntity(entry.Entity);
            }

            await _action(entry.Entity);

            // Check if the entity is still scheduled.
            if (_entries.ContainsKey(entry.Entity))
            {
                entry.SetTimer();
            }
        }

        private class SchedulerEntry
        {
            private long _period;

            public SchedulerEntry(T entity, long period, TimerCallback timerCallback, bool autoStart, ITimerFactory timerFactory)
            {
                Entity = entity;
                _period = period;
                Timer = timerFactory.CreateTimer(timerCallback, this, autoStart ? period : Timeout.Infinite, Timeout.Infinite);
            }

            public T Entity { get; }

            public long Period => _period;

            public ITimer Timer { get; }

            public void ChangePeriod(long newPeriod, bool resetTimer)
            {
                Interlocked.Exchange(ref _period, newPeriod);
                if (resetTimer)
                {
                    SetTimer();
                }
            }

            public void SetTimer()
            {
                try
                {
                    Timer.Change(Interlocked.Read(ref _period), Timeout.Infinite);
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

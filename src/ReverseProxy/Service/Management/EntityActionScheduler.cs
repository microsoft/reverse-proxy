// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.Management
{
    /// <summary>
    /// Periodically invokes specified actions on registered entities.
    /// </summary>
    internal sealed class EntityActionScheduler<T> : IDisposable
    {
        private readonly Action<T> _action;
        private readonly bool _runOnce;
        private readonly Dictionary<T, LinkedListNode<SchedulerEntry>> _map = new Dictionary<T, LinkedListNode<SchedulerEntry>>();
        private readonly LinkedList<SchedulerEntry> _list = new LinkedList<SchedulerEntry>();
        private readonly Timer _timer;
        private readonly IUptimeClock _clock;
        private bool _isStarted;
        private readonly object _syncRoot = new object();

        public EntityActionScheduler(Action<T> action, bool autoStart, bool runOnce, IUptimeClock clock)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _runOnce = runOnce;
            _isStarted = autoStart;
            _timer = new Timer(_ => Run(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public IEnumerable<T> GetScheduledEntities()
        {
            lock (_syncRoot)
            {
                return _map.Keys.ToArray();
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        public void Start()
        {
            lock (_syncRoot)
            {
                if (_isStarted)
                {
                    return;
                }

                // Update entries RunAt due to a delayed start to avoid triggering all of them immediately.
                var now = _clock.TickCount;
                var next = _list.First;
                while (next != null)
                {
                    var currentEntry = next.Value;
                    next.Value = new SchedulerEntry(currentEntry.Entity, currentEntry.Period, now + currentEntry.Period);
                    next = next.Next;
                }

                _isStarted = true;
                ScheduleNextRun(now);
            }
        }

        public void ScheduleEntity(T entity, TimeSpan period)
        {
            lock (_syncRoot)
            {
                if (_map.TryGetValue(entity, out var node))
                {
                    // We don't allow repeated registrations of the same entity and apply "first write wins" strategy
                    // to avoid triggering multiple action runs for the same event.
                    return;
                }

                InsertAndAdjustTimer(entity, (long)period.TotalMilliseconds);
            }
        }

        public void ChangePeriod(T entity, TimeSpan newPeriod)
        {
            lock (_syncRoot)
            {
                if (_map.TryGetValue(entity, out var node))
                {
                    _list.Remove(node);
                }

                InsertAndAdjustTimer(entity, (long)newPeriod.TotalMilliseconds);
            }
        }

        public void UnscheduleEntity(T entity)
        {
            lock (_syncRoot)
            {
                if (_map.Remove(entity, out var node))
                {
                    _list.Remove(node);
                }

                if (_list.First == null)
                {
                    // The last node was removed, so we need to disable timer.
                    RestartTimer(Timeout.Infinite);
                }
            }
        }

        private void Run()
        {
            SchedulerEntry triggeredEntry;
            var runAction = false;
            lock (_syncRoot)
            {
                if (_list.First == null)
                {
                    return;
                }

                var cutoff = _clock.TickCount;
                triggeredEntry = _list.First.Value;
                if (triggeredEntry.RunAt <= cutoff)
                {
                    runAction = true;
                    if (_runOnce)
                    {
                        _map.Remove(triggeredEntry.Entity);
                    }
                    else
                    {
                        InsertNewNode(triggeredEntry.Entity, triggeredEntry.Period, cutoff + triggeredEntry.Period);
                    }

                    _list.RemoveFirst();
                }

                ScheduleNextRun(cutoff);
            }

            // Don't invoke callback if it's too soon.
            // This can happen if an entry was removed concurrently with the timer firing.
            if (runAction)
            {
                _action(triggeredEntry.Entity);
            }
        }

        private void ScheduleNextRun(long cutoff)
        {
            // Assume the lock is being held.
            if (_list.First != null)
            {
                var newDueTime = _list.First.Value.RunAt >= cutoff ? _list.First.Value.RunAt - cutoff : 0;
                RestartTimer(newDueTime);
            }
        }

        private void InsertAndAdjustTimer(T entity, long period)
        {
            // Assume the lock is being held.
            var newNode = InsertNewNode(entity, period, _clock.TickCount + period);

            if (ReferenceEquals(newNode, _list.First))
            {
                // The first node was added, so we need to change timer.
                RestartTimer(period);
            }
        }

        private LinkedListNode<SchedulerEntry> InsertNewNode(T entity, long period, long newRunAt)
        {
            // Assume the lock is being held.
            // Go from an entry with the most distant firing time to an entry with the soonest one (from Last to First).
            // Insert a new entry either right after an entry with the closest but still lower (closer to 'now') firing time
            // or as the new First if the new entry is the soonest among all others on the list.
            var previous = _list.Last;
            while (previous != null && previous.Value.RunAt > newRunAt)
            {
                previous = previous.Previous;
            }

            var newEntry = new SchedulerEntry(entity, period, newRunAt);
            LinkedListNode<SchedulerEntry> newNode;
            if (previous != null)
            {
                newNode = _list.AddAfter(previous, newEntry);
            }
            else
            {
                newNode = _list.AddFirst(newEntry);
            }

            _map[entity] = newNode;
            return newNode;
        }

        private void RestartTimer(long dueTime)
        {
            // Assume the lock is being held.
            if (_isStarted)
            {
                _timer.Change(dueTime, Timeout.Infinite);
            }
        }

        private readonly struct SchedulerEntry
        {
            public SchedulerEntry(T entity, long period, long runAt)
            {
                Entity = entity;
                RunAt = runAt;
                Period = period;
            }

            public T Entity { get; }

            public long Period { get; }

            public long RunAt { get; }
        }
    }
}

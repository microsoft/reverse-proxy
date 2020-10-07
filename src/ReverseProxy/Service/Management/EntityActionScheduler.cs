// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.Management
{
    /// <summary>
    /// Periodically invokes specified actions on registered entities.
    /// </summary>
    public sealed class EntityActionScheduler<T> : IDisposable
    {
        private readonly Action<T> _action;
        private readonly bool _runOnce;
        private readonly Dictionary<T, LinkedListNode<SchedulerEntry>> _map = new Dictionary<T, LinkedListNode<SchedulerEntry>>();
        private readonly LinkedList<SchedulerEntry> _list = new LinkedList<SchedulerEntry>();
        private readonly Timer _timer;
        private readonly IUptimeClock _clock;
        private readonly object _syncRoot = new object();

        public EntityActionScheduler(Action<T> action, bool runOnce, IUptimeClock clock)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _runOnce = runOnce;
            _timer = new Timer(_ => Run(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Dispose()
        {
            _timer.Dispose();
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
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }

        private void InsertAndAdjustTimer(T entity, long period)
        {
            var newNode = InsertNewNode(entity, period, _clock.TickCount + period);

            if (ReferenceEquals(newNode, _list.First))
            {
                // The first node was added, so we need to change timer.
                _timer.Change(period, Timeout.Infinite);
            }
        }

        private LinkedListNode<SchedulerEntry> InsertNewNode(T entity, long period, long newRunAt)
        {
            var next = _list.First;
            while (next != null && next.Value.RunAt < newRunAt)
            {
                next = next.Next;
            }

            var newEntry = new SchedulerEntry(entity, period, newRunAt);
            LinkedListNode<SchedulerEntry> newNode;
            if (next != null)
            {
                newNode = _list.AddBefore(next, newEntry);
            }
            else
            {
                newNode = _list.AddFirst(newEntry);
            }

            _map[entity] = newNode;
            return newNode;
        }

        private void Run()
        {
            SchedulerEntry triggeredEntry;
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

                if (_list.First != null)
                {
                    _timer.Change(_list.First.Value.RunAt - cutoff, Timeout.Infinite);
                }
            }

            _action(triggeredEntry.Entity);
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

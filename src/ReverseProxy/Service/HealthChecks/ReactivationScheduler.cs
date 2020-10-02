// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class ReactivationScheduler : IReactivationScheduler
    {
        private Dictionary<DestinationInfo, LinkedListNode<ReactivationEntry>> _map = new Dictionary<DestinationInfo, LinkedListNode<ReactivationEntry>>();
        private LinkedList<ReactivationEntry> _list = new LinkedList<ReactivationEntry>();
        private Timer _timer;
        private readonly object _syncRoot = new object();

        public ReactivationScheduler()
        {
            _timer = new Timer(_ => Reactivate(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void ScheduleRestoringAsHealthy(DestinationInfo destination, TimeSpan reactivationPeriod)
        {
            lock (_syncRoot)
            {
                var period = (long) reactivationPeriod.TotalMilliseconds;
                var newReactivateAt = Environment.TickCount64 + period;
                LinkedListNode<ReactivationEntry> newNode = null;
                LinkedListNode<ReactivationEntry> next = null;
                if (_map.TryGetValue(destination, out var node))
                {
                    if (newReactivateAt > node.Value.ReactivateAt)
                    {
                        // Only the soonest reactivation takes effect to avoid delaying for too long in case of sequential registrations.
                        return;
                    }

                    _list.Remove(node);
                    
                    next = _list.First;
                    while (next != null && next.Value.ReactivateAt < newReactivateAt)
                    {
                        next = next.Next;
                    }
                }
                
                var newEntry = new ReactivationEntry(destination, newReactivateAt);
                if (next != null)
                {
                    newNode = _list.AddBefore(next, newEntry);
                }
                else
                {
                    newNode = _list.AddFirst(newEntry);
                }
                
                _map[destination] = newNode;

                if (ReferenceEquals(newNode, _list.First))
                {
                    // The first node was added, so we need to change timer.
                    _timer.Change(period, Timeout.Infinite);
                }
            }
        }

        private void Reactivate()
        {
            lock (_syncRoot)
            {
                var cutoff = Environment.TickCount64;
                var next = _list.First;
                while (next != null && next.Value.ReactivateAt <= cutoff)
                {
                    var destination = next.Value.Destination;
                    destination.DynamicStateSignal.Value = new DestinationDynamicState(destination.DynamicState.Health.ChangePassive(DestinationHealth.Healthy));
                    next = next.Next;
                    _map.Remove(destination);
                }

                if (next != null)
                {
                    _timer.Change(next.Value.ReactivateAt - cutoff, Timeout.Infinite);
                }
            }
        }
        
        private readonly struct ReactivationEntry
        {
            public ReactivationEntry(DestinationInfo destination, long reactivateAt)
            {
                Destination = destination;
                ReactivateAt = reactivateAt;
            }

            public DestinationInfo Destination { get; }

            public long ReactivateAt { get; }
        }
    }
}
// <copyright file="VirtualMonotonicTimer.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Common.Abstractions.Time;

namespace Tests.Common
{
    /// <summary>
    /// Simulation analog to MonotonicTimer, used for testing.
    /// </summary>
    /// <remarks>
    /// This timer doesn't track real time, but instead tracks virtual. Time only advances when
    /// the <see cref="AdvanceClockBy"/> method is called.
    /// <para/>
    /// </remarks>
    public class VirtualMonotonicTimer : IMonotonicTimer
    {
        private readonly SortedList<TimeSpan, DelayItem> _delayItems = new SortedList<TimeSpan, DelayItem>();

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualMonotonicTimer" /> class.
        /// </summary>
        /// <param name="initialTime">Initial value for current time. Zero if not specified.</param>
        public VirtualMonotonicTimer(TimeSpan? initialTime = null)
        {
            this.CurrentTime = initialTime ?? TimeSpan.Zero;
        }

        /// <inheritdoc/>
        public TimeSpan CurrentTime { get; private set; }

        /// <summary>
        /// Advances time by the specified amount.
        /// </summary>
        /// <param name="howMuch">How much to advance <see cref="CurrentTime"/> by.</param>
        public void AdvanceClockBy(TimeSpan howMuch)
        {
            this.AdvanceClockTo(this.CurrentTime + howMuch);
        }

        /// <summary>
        /// Advances time to the specified point.
        /// </summary>
        /// <param name="targetTime">Advances <see cref="CurrentTime"/> until it equals <paramref name="targetTime"/>.</param>
        public void AdvanceClockTo(TimeSpan targetTime)
        {
            if (targetTime < this.CurrentTime)
            {
                throw new InvalidOperationException("Time should not flow backwards");
            }

            // Signal any delays that have expired by advancing the clock.
            while (this._delayItems.Count > 0 && this._delayItems.ElementAt(0).Key <= targetTime)
            {
                this.AdvanceStep();
            }

            this.CurrentTime = targetTime;
        }

        /// <summary>
        /// Creates a task that completes when CurrentTime >= expiryTime.
        /// </summary>
        /// <param name="expiryTime">Time at which the returned task will be completed.</param>
        /// <param name="cancelationToken">Cancellation token for the created task.</param>
        /// <returns>A task which completed at <paramref name="expiryTime"/>.</returns>
        public async Task DelayUntil(TimeSpan expiryTime, CancellationToken cancelationToken)
        {
            if (expiryTime <= this.CurrentTime)
            {
                return;
            }

            var delayTask = new DelayItem
            {
                When = expiryTime,
                Signal = new TaskCompletionSource<int>(cancelationToken),
            };

            var task = delayTask.Signal.Task;

            // Note: sorted list doesn't allow duplicates, so increment expiry until unique.
            while (this._delayItems.ContainsKey(expiryTime))
            {
                expiryTime += TimeSpan.FromTicks(1);
            }

            this._delayItems.Add(expiryTime, delayTask);

            using (cancelationToken.Register(() => this.CancelTask(delayTask)))
            {
                await task;
            }
        }

        /// <summary>
        /// Advances time to schedule the next item of work.
        /// </summary>
        /// <returns>True if any timers were found and signaled.</returns>
        public bool AdvanceStep()
        {
            if (this._delayItems.Count > 0)
            {
                var next = this._delayItems.ElementAt(0);
                this.CurrentTime = next.Key;

                // Note: this will unfortunately have O(N) cost. However, this code is only used for testing right now, and the list is generally short. If that
                // ever changes, suggest finding a priority queue / heap data structure for .Net (core libraries are lacking this data structure).
                this._delayItems.RemoveAt(0);

                // Unblock the task. It's no longer asleep.
                next.Value.Signal.TrySetResult(0);

                // Note that TPL normally schedules tasks synchronously. When used with
                // the SingleThreadedTaskScheduler, we can assume all tasks have completed by the
                // time SetResult returns, provided that AdvanceClockTo was invoked outside of the task scheduler
                // loop.
                return true;
            }

            return false;
        }

        private void CancelTask(DelayItem delayTask)
        {
            var i = this._delayItems.IndexOfValue(delayTask);
            if (i != -1)
            {
                this._delayItems.RemoveAt(i);
            }

            delayTask.Signal.TrySetCanceled();
        }

        private class DelayItem
        {
            public TimeSpan When { get; set; }

            public TaskCompletionSource<int> Signal { get; set; }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Internal;
using Microsoft.Kubernetes.Controller.Queues.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA1001 // Type owns disposable fields but is not disposable
#pragma warning disable CA2213 // Disposable fields should be disposed

namespace Microsoft.Kubernetes.Controller.Queues;

/// <summary>
/// Class DelayingQueue is the default implementation of <see cref="IDelayingQueue{TItem}" />.
/// Implements the <see cref="Composition.WorkQueueBase{TItem}" />.
/// Implements the <see cref="IDelayingQueue{TItem}" />.
/// </summary>
/// <typeparam name="TItem">The type of the t item.</typeparam>
/// <seealso cref="Composition.WorkQueueBase{TItem}" />
/// <seealso cref="IDelayingQueue{TItem}" />
public class DelayingQueue<TItem> : Composition.WorkQueueBase<TItem>, IDelayingQueue<TItem>
{
    private readonly CancellationTokenSource _shuttingDown = new CancellationTokenSource();
    private readonly Channel<WaitFor> _waitingForAddCh = new Channel<WaitFor>(new List<WaitFor>());
    private readonly Task _waitingLoopTask;
    private readonly ISystemClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelayingQueue{TItem}" /> class.
    /// </summary>
    public DelayingQueue(ISystemClock clock = default, IWorkQueue<TItem> @base = default)
        : base(@base ?? new WorkQueue<TItem>())
    {
        _waitingLoopTask = Task.Run(WaitingLoopAsync);
        _clock = clock ?? new SystemClock(); ;
    }

    /// <summary>
    /// Shuts down.
    /// </summary>
    public override void ShutDown()
    {
        _shuttingDown.Cancel();
        base.ShutDown();
    }

    /// <summary>
    /// Adds the after.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="delay">The delay.</param>
    public virtual void AddAfter(TItem item, TimeSpan delay)
    {
        // util\workqueue\delaying_queue.go:160
        if (ShuttingDown())
        {
            return;
        }

        // COUNTER: retry
        if (delay.TotalMilliseconds <= 0)
        {
            Add(item);
            return;
        }

        _waitingForAddCh.Push(new WaitFor { Item = item, ReadyAt = _clock.UtcNow + delay });
    }

    /// <summary>
    /// Background loop for delaying queue. Will continuously evaluate when next
    /// items should be added. Async await sleeps until that time. Also wakes up when
    /// new items are pushed onto queue, or when <see cref="ShutDown" /> is called.
    /// </summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public async Task WaitingLoopAsync()
    {
        // util\workqueue\delaying_queue.go:187
        var waitingForQueue = new Heap<WaitFor>(
            new List<WaitFor>(),
            Comparer<WaitFor>.Create(WaitFor.Compare));

        while (!ShuttingDown())
        {
            var now = _clock.UtcNow;
            var nextReadyAtDelay = TimeSpan.FromSeconds(10);

            // remove and add all of the items that are readyAt by now
            // stop and calculate delay for the first one note readyAt by now
            // default to 10 seconds delay if the queue is fully emptied
            while (waitingForQueue.TryPeek(out var waitEntry))
            {
                if (waitEntry.ReadyAt > now)
                {
                    nextReadyAtDelay = waitEntry.ReadyAt - now;
                    break;
                }
                else
                {
                    Add(waitingForQueue.Pop().Item);
                }
            }

            // await for a delay, shuttingDown signal, or when addition items need to be queued
            var nextReadyAtDelayTask = Task.Delay(nextReadyAtDelay, _shuttingDown.Token);
            var waitingForAddChTask = _waitingForAddCh.WaitAsync();
            var whenTask = await Task.WhenAny(
                waitingForAddChTask,
                nextReadyAtDelayTask);

            // await the condition as well, just in case there's an exception to observe
            await whenTask;

            if (whenTask == waitingForAddChTask)
            {
                now = _clock.UtcNow;
                while (_waitingForAddCh.TryPop(out var waitEntry))
                {
                    if (waitEntry.ReadyAt > now)
                    {
                        waitingForQueue.Push(waitEntry);
                    }
                    else
                    {
                        Add(waitEntry.Item);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Struct WaitFor holds an item while queued for a time in the future.
    /// </summary>
    private struct WaitFor
    {
        /// <summary>
        /// The item to be queued in the future.
        /// </summary>
        public TItem Item;

        /// <summary>
        /// The soonest UTC time the item is ready to be queued.
        /// </summary>
        public DateTimeOffset ReadyAt;

        public static int Compare(WaitFor first, WaitFor second) => DateTimeOffset.Compare(first.ReadyAt, second.ReadyAt);
    }
}

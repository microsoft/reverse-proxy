// <copyright file="SignalContext.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;

namespace IslandGateway.Signals
{
    /// <summary>
    /// Represents the context in which a signal was created.
    /// Signal operations can only be performed among signals in the same context
    /// to ensure thread safety.
    /// </summary>
    /// <remarks>
    /// This implementation ensures that writes to signals are sequentialized
    /// and processed in a single thread.
    /// </remarks>
    public sealed class SignalContext
    {
        private readonly Queue<Action> workQueue = new Queue<Action>();
        private bool isExecuting;

        internal SignalContext()
        {
        }

        /// <summary>
        /// Queues the given <paramref name="action"/> for execution.
        /// </summary>
        internal void QueueAction(Action action)
        {
            lock (this.workQueue)
            {
                this.workQueue.Enqueue(action);
                this.Execute();
            }
        }

        private void Execute()
        {
            // NOTE: We are already within the lock when this method is called.
            if (this.isExecuting)
            {
                // Prevent reentrancy. Reentrancy can lead to stack overflow and difficulty when debugging.
                return;
            }

            this.isExecuting = true;
            try
            {
                while (true)
                {
                    if (this.workQueue.Count == 0)
                    {
                        return;
                    }

                    var action = this.workQueue.Dequeue();
                    action();
                }
            }
            finally
            {
                this.isExecuting = false;
            }
        }
    }
}

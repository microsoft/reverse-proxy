// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        private readonly Queue<Action> _workQueue = new Queue<Action>();
        private bool _isExecuting;

        internal SignalContext()
        {
        }

        /// <summary>
        /// Queues the given <paramref name="action"/> for execution.
        /// </summary>
        internal void QueueAction(Action action)
        {
            lock (_workQueue)
            {
                _workQueue.Enqueue(action);
                Execute();
            }
        }

        private void Execute()
        {
            // NOTE: We are already within the lock when this method is called.
            if (_isExecuting)
            {
                // Prevent reentrancy. Reentrancy can lead to stack overflow and difficulty when debugging.
                return;
            }

            _isExecuting = true;
            try
            {
                while (true)
                {
                    if (_workQueue.Count == 0)
                    {
                        return;
                    }

                    var action = _workQueue.Dequeue();
                    action();
                }
            }
            finally
            {
                _isExecuting = false;
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Controller.Queues.Utils;

/// <summary>
/// Class Channel is a utility to facilitate pushing data from one thread to
/// be popped by another async thread.
/// </summary>
/// <typeparam name="T">The type of item this channel contains.</typeparam>
public class Channel<T>
{
    private readonly IList<T> _collection;
    private readonly object _sync = new object();
    private TaskCompletionSource<int> _tcs = new TaskCompletionSource<int>();

    /// <summary>
    /// Initializes a new instance of the <see cref="Channel{T}" /> class.
    /// </summary>
    /// <param name="collection">The collection.</param>
    public Channel(IList<T> collection)
    {
        _collection = collection;
    }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="Channel{T}" />.
    /// </summary>
    /// <value>The number of elements.</value>
    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _collection.Count;
            }
        }
    }

    /// <summary>
    /// Pushes the specified item onto the channel. If the channel
    /// was previously empty the async Task is also marked compete to
    /// unblock an awaiting thread.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Push(T item)
    {
        lock (_sync)
        {
            _collection.Add(item);
            if (_collection.Count == 1)
            {
                _tcs.SetResult(0);
            }
        }
    }

    /// <summary>
    /// Pops an item off of the channel. Must not be called when channel is empty.
    /// </summary>
    /// <returns>The item.</returns>
    public T Pop()
    {
        lock (_sync)
        {
            int index = _collection.Count - 1;
            var item = _collection[index];
            _collection.RemoveAt(index);
            if (index == 0)
            {
                _tcs = new TaskCompletionSource<int>();
            }

            return item;
        }
    }

    /// <summary>
    /// Tries to Pop an item off of the channel.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><c>true</c> if item is returned, <c>false</c> otherwise.</returns>
    public bool TryPop(out T item)
    {
        lock (_sync)
        {
            if (_collection.Count == 0)
            {
                item = default;
                return false;
            }

            item = Pop();
            return true;
        }
    }

    /// <summary>
    /// Called to know when data is available on the channel.
    /// </summary>
    /// <returns>An awaitable Task.</returns>
    public Task WaitAsync()
    {
        lock (_sync)
        {
            return _tcs.Task;
        }
    }
}

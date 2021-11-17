// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.Kubernetes.Controller.Dispatching;

namespace Yarp.Kubernetes.Controller.Services;

/// <summary>
/// QueueItem acts as the "Key" for the _queue to manage items.
/// </summary>
public struct QueueItem : IEquatable<QueueItem>
{
    public QueueItem(string change, IDispatchTarget dispatchTarget)
    {
        Change = change;
        DispatchTarget = dispatchTarget;
    }

    /// <summary>
    /// This identifies that a change has occured and either configuration requires to be rebuilt, or needs to be dispatched.
    /// </summary>
    public string Change { get; }

    /// <summary>
    /// This idenitifies a single target if the work item is caused by a new connection, otherwise null
    /// if the information should be sent to all current connections.
    /// </summary>
    public IDispatchTarget DispatchTarget { get; }

    public override bool Equals(object obj)
    {
        return obj is QueueItem item && Equals(item);
    }

    public bool Equals(QueueItem other)
    {
        return Change.Equals(other.Change, StringComparison.Ordinal) &&
               EqualityComparer<IDispatchTarget>.Default.Equals(DispatchTarget, other.DispatchTarget);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Change, DispatchTarget);
    }

    public static bool operator ==(QueueItem left, QueueItem right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(QueueItem left, QueueItem right)
    {
        return !(left == right);
    }
}

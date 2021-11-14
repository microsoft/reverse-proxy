// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Kubernetes;
using Yarp.Kubernetes.Controller.Dispatching;

namespace Yarp.Kubernetes.Controller.Services;

/// <summary>
/// QueueItem acts as the "Key" for the _queue to manage items.
/// </summary>
public struct QueueItem : IEquatable<QueueItem>
{
    public QueueItem(NamespacedName namespacedName, IDispatchTarget dispatchTarget)
    {
        NamespacedName = namespacedName;
        DispatchTarget = dispatchTarget;
    }

    /// <summary>
    /// This identifies an Ingress which must be dispatched because it, or a related resource, has changed.
    /// </summary>
    public NamespacedName NamespacedName { get; }

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
        return NamespacedName.Equals(other.NamespacedName) &&
               EqualityComparer<IDispatchTarget>.Default.Equals(DispatchTarget, other.DispatchTarget);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(NamespacedName, DispatchTarget);
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

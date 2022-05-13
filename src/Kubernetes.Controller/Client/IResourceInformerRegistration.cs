// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Client;

/// <summary>
/// Returned by <see cref="IResourceInformer{TResource}.Register(ResourceInformerCallback{TResource})"/> to control the lifetime of an event
/// notification connection. Call <see cref="IDisposable.Dispose()"/> when the lifetime of the notification receiver is ending.
/// </summary>
public interface IResourceInformerRegistration : IDisposable
{
    /// <summary>
    /// Returns a task that can be awaited to know when the initial listing of resources is complete.
    /// Once an await on this method it is safe to assume that all of the knowledge of this resource
    /// type has been made available, and everything going forward will be updatres.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>Task.</returns>
    Task ReadyAsync(CancellationToken cancellationToken);
}

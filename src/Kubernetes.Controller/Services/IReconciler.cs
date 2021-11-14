// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes;
using System;
using System.Threading;
using System.Threading.Tasks;
using Yarp.Kubernetes.Controller.Dispatching;

namespace Yarp.Kubernetes.Controller.Services;

/// <summary>
/// IReconciler is a service interface called by the <see cref="IngressController"/> to process
/// the work items as they are dequeued.
/// </summary>
public interface IReconciler
{
    void OnAttach(Action<IDispatchTarget> attached);
    Task ProcessAsync(IDispatchTarget target, NamespacedName key, ReconcileData data, CancellationToken cancellationToken);
}

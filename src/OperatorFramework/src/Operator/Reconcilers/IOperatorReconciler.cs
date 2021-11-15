// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Operator.Reconcilers;

/// <summary>
/// Interface IOperatorReconciler.
/// </summary>
/// <typeparam name="TResource">The type of the t resource.</typeparam>
public interface IOperatorReconciler<TResource>
    where TResource : class, IKubernetesObject<V1ObjectMeta>
{
    /// <summary>
    /// Reconciles the specified resource.
    /// </summary>
    /// <param name="parameters">The information about desired and current state of cluster resources.</param>
    /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>Results of the reconcile.</returns>
    Task<ReconcileResult> ReconcileAsync(ReconcileParameters<TResource> parameters, CancellationToken cancellationToken);
}

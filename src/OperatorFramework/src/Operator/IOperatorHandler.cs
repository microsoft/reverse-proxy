// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Operator
{
    public interface IOperatorHandler<TResource> : IDisposable
        where TResource : class, IKubernetesObject<V1ObjectMeta>
    {
        Task RunAsync(CancellationToken cancellationToken);
    }
}

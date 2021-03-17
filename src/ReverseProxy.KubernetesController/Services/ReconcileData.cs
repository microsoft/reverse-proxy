// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Yarp.ReverseProxy.KubernetesController.Caching;

namespace Yarp.ReverseProxy.KubernetesController.Services
{
    /// <summary>
    /// ReconcileData is the information returned from <see cref="ICache.TryGetReconcileData(Microsoft.Kubernetes.NamespacedName, out ReconcileData)"/>
    /// and needed by <see cref="IReconciler.ProcessAsync(Dispatching.IDispatchTarget, Microsoft.Kubernetes.NamespacedName, ReconcileData, System.Threading.CancellationToken)"/>.
    /// </summary>
    public struct ReconcileData
    {
        public IngressData Ingress { get; set; }
        public List<Endpoints> EndpointsList { get; set; }
    }
}

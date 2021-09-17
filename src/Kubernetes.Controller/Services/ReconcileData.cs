// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Yarp.Kubernetes.Controller.Caching;

namespace Yarp.Kubernetes.Controller.Services
{
    /// <summary>
    /// ReconcileData is the information returned from <see cref="ICache.TryGetReconcileData(Microsoft.Kubernetes.NamespacedName, out ReconcileData)"/>
    /// and needed by <see cref="IReconciler.ProcessAsync(Dispatching.IDispatchTarget, Microsoft.Kubernetes.NamespacedName, ReconcileData, System.Threading.CancellationToken)"/>.
    /// </summary>
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct ReconcileData
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public ReconcileData(IngressData ingress, List<ServiceData> services, List<Endpoints> endpoints)
        {
            Ingress = ingress;
            ServiceList = services;
            EndpointsList = endpoints;
        }

        public IngressData Ingress { get; }
        public List<ServiceData> ServiceList { get; }
        public List<Endpoints> EndpointsList { get; }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Kubernetes;
using System.Collections.Generic;
using System.Collections.Immutable;
using Yarp.ReverseProxy.KubernetesController.Services;

namespace Yarp.ReverseProxy.KubernetesController.Caching
{
    /// <summary>
    /// ICache service interface holds onto least amount of data necessary
    /// for <see cref="IReconciler"/> to process work.
    /// </summary>
    public interface ICache
    {
        void Update(WatchEventType eventType, V1Ingress ingress);
        ImmutableList<string> Update(WatchEventType eventType, V1Endpoints endpoints);
        bool TryGetReconcileData(NamespacedName key, out ReconcileData data);
        void GetKeys(List<NamespacedName> keys);
    }
}

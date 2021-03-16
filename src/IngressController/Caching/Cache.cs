// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Kubernetes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using IngressController.Services;

namespace IngressController.Caching
{
    /// <summary>
    /// ICache service interface holds onto least amount of data necessary
    /// for <see cref="IReconciler"/> to process work.
    /// </summary>
    public class Cache : ICache
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, NamespaceCache> _namespaceCaches = new Dictionary<string, NamespaceCache>();

        public void Update(WatchEventType eventType, V1Ingress ingress)
        {
            if (ingress is null)
            {
                throw new ArgumentNullException(nameof(ingress));
            }

            Namespace(ingress.Namespace()).Update(eventType, ingress);
        }

        public ImmutableList<string> Update(WatchEventType eventType, V1Endpoints endpoints)
        {
            return Namespace(endpoints.Namespace()).Update(eventType, endpoints);
        }

        public bool TryGetReconcileData(NamespacedName key, out ReconcileData data)
        {
            return Namespace(key.Namespace).TryLookup(key, out data); ;
        }

        public void GetKeys(List<NamespacedName> keys)
        {
            lock (_sync)
            {
                foreach (var (ns, cache) in _namespaceCaches)
                {
                    cache.GetKeys(ns, keys);
                }
            }
        }


        private NamespaceCache Namespace(string key)
        {
            lock (_sync)
            {
                if (!_namespaceCaches.TryGetValue(key, out var value))
                {
                    value = new NamespaceCache();
                    _namespaceCaches.Add(key, value);
                }
                return value;
            }
        }
    }
}

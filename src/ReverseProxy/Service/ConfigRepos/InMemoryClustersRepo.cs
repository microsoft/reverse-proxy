// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.Service
{
    internal class InMemoryClustersRepo : IClustersRepo
    {
        private readonly object _syncRoot = new object();
        private IDictionary<string, Cluster> _items;

        /// <inheritdoc/>
        public Task<IDictionary<string, Cluster>> GetClustersAsync(CancellationToken cancellation)
        {
            return Task.FromResult(Get());
        }

        /// <inheritdoc/>
        public Task SetClustersAsync(IDictionary<string, Cluster> clusters, CancellationToken cancellation)
        {
            Set(clusters);
            return Task.CompletedTask;
        }

        protected IDictionary<string, Cluster> Get()
        {
            lock (_syncRoot)
            {
                return _items?.DeepClone(StringComparer.Ordinal);
            }
        }

        protected void Set(IDictionary<string, Cluster> items)
        {
            lock (_syncRoot)
            {
                _items = items?.DeepClone(StringComparer.Ordinal);
            }
        }
    }
}

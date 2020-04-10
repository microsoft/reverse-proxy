// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Core.Abstractions;

namespace Microsoft.ReverseProxy.Core.Service
{
    internal class InMemoryBackendsRepo : IBackendsRepo
    {
        private readonly object _syncRoot = new object();
        private IDictionary<string, Backend> _items;

        /// <inheritdoc/>
        public Task<IDictionary<string, Backend>> GetBackendsAsync(CancellationToken cancellation)
        {
            return Task.FromResult(Get());
        }

        /// <inheritdoc/>
        public Task SetBackendsAsync(IDictionary<string, Backend> backends, CancellationToken cancellation)
        {
            Set(backends);
            return Task.CompletedTask;
        }

        protected IDictionary<string, Backend> Get()
        {
            lock (_syncRoot)
            {
                return _items?.DeepClone(StringComparer.Ordinal);
            }
        }

        protected void Set(IDictionary<string, Backend> items)
        {
            lock (_syncRoot)
            {
                _items = items?.DeepClone(StringComparer.Ordinal);
            }
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.Service
{
    internal class InMemoryEndpointsRepo : IBackendEndpointsRepo
    {
        private readonly object _lockObject = new object();
        private readonly Dictionary<string, IList<BackendEndpoint>> _backendEndpoints = new Dictionary<string, IList<BackendEndpoint>>(StringComparer.Ordinal);

        /// <inheritdoc/>
        public Task<IList<BackendEndpoint>> GetEndpointsAsync(string backendId, CancellationToken cancellation)
        {
            Contracts.CheckNonEmpty(backendId, nameof(backendId));

            lock (_lockObject)
            {
                _backendEndpoints.TryGetValue(backendId, out var results);
                return Task.FromResult(results?.DeepClone());
            }
        }

        /// <inheritdoc/>
        public Task SetEndpointsAsync(string backendId, IList<BackendEndpoint> endpoints, CancellationToken cancellation)
        {
            Contracts.CheckNonEmpty(backendId, nameof(backendId));

            lock (_lockObject)
            {
                _backendEndpoints[backendId] = endpoints?.DeepClone();
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task RemoveEndpointsAsync(string backendId, CancellationToken cancellation)
        {
            Contracts.CheckNonEmpty(backendId, nameof(backendId));

            lock (_lockObject)
            {
                _backendEndpoints.Remove(backendId);
            }

            return Task.CompletedTask;
        }
    }
}

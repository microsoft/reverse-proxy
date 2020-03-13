// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Core.Abstractions;

namespace IslandGateway.Core.Service
{
    internal class InMemoryBackendsRepo : InMemoryListBase<Backend>, IBackendsRepo
    {
        /// <inheritdoc/>
        public Task<IList<Backend>> GetBackendsAsync(CancellationToken cancellation)
        {
            return Task.FromResult(Get());
        }

        /// <inheritdoc/>
        public Task SetBackendsAsync(IList<Backend> backends, CancellationToken cancellation)
        {
            Set(backends);
            return Task.CompletedTask;
        }
    }
}

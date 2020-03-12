// <copyright file="InMemoryBackendsRepo.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

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
            return Task.FromResult(this.Get());
        }

        /// <inheritdoc/>
        public Task SetBackendsAsync(IList<Backend> backends, CancellationToken cancellation)
        {
            this.Set(backends);
            return Task.CompletedTask;
        }
    }
}

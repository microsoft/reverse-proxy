// <copyright file="InMemoryRoutesRepo.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Core.Abstractions;

namespace IslandGateway.Core.Service
{
    internal class InMemoryRoutesRepo : InMemoryListBase<GatewayRoute>, IRoutesRepo
    {
        /// <inheritdoc/>
        public Task<IList<GatewayRoute>> GetRoutesAsync(CancellationToken cancellation)
        {
            return Task.FromResult(Get());
        }

        /// <inheritdoc/>
        public Task SetRoutesAsync(IList<GatewayRoute> routes, CancellationToken cancellation)
        {
            Set(routes);
            return Task.CompletedTask;
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

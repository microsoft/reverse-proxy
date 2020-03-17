// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IslandGateway.Core.Abstractions
{
    /// <summary>
    /// Manages the set of routes. Changes only become effective when
    /// <see cref="IIslandGatewayConfigManager.ApplyConfigurationsAsync"/> is called.
    /// </summary>
    public interface IRoutesRepo
    {
        /// <summary>
        /// Gets the current set of routes.
        /// </summary>
        Task<IList<GatewayRoute>> GetRoutesAsync(CancellationToken cancellation);

        /// <summary>
        /// Sets the current set of routes.
        /// </summary>
        Task SetRoutesAsync(IList<GatewayRoute> routes, CancellationToken cancellation);
    }
}

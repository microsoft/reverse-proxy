// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Core.Abstractions
{
    /// <summary>
    /// Manages the set of backend endpoints. Changes only become effective when
    /// <see cref="IIslandGatewayConfigManager.ApplyConfigurationsAsync"/> is called.
    /// </summary>
    public interface IBackendEndpointsRepo
    {
        /// <summary>
        /// Gets the set of endpoints for the given <paramref name="backendId"/>.
        /// </summary>
        Task<IList<BackendEndpoint>> GetEndpointsAsync(string backendId, CancellationToken cancellation);

        /// <summary>
        /// Sets a new set of backend endpoints for the given <paramref name="backendId"/>.
        /// </summary>
        Task SetEndpointsAsync(string backendId, IList<BackendEndpoint> endpoints, CancellationToken cancellation);

        /// <summary>
        /// Removes all endpoints tracked for the provided <paramref name="backendId"/>.
        /// </summary>
        Task RemoveEndpointsAsync(string backendId, CancellationToken cancellation);
    }
}

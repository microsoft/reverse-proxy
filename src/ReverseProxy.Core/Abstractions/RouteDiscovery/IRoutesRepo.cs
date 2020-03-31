// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Core.Abstractions
{
    /// <summary>
    /// Manages the set of routes. Changes only become effective when
    /// <see cref="IReverseProxyConfigManager.ApplyConfigurationsAsync"/> is called.
    /// </summary>
    public interface IRoutesRepo
    {
        /// <summary>
        /// Gets the current set of routes.
        /// </summary>
        Task<IList<ProxyRoute>> GetRoutesAsync(CancellationToken cancellation);

        /// <summary>
        /// Sets the current set of routes.
        /// </summary>
        Task SetRoutesAsync(IList<ProxyRoute> routes, CancellationToken cancellation);
    }
}

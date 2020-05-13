// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Manages the set of backends. Changes only become effective when
    /// <see cref="IReverseProxyConfigManager.ApplyConfigurationsAsync"/> is called.
    /// </summary>
    public interface IBackendsRepo
    {
        /// <summary>
        /// Gets the current set of backends.
        /// </summary>
        Task<IDictionary<string, Backend>> GetBackendsAsync(CancellationToken cancellation);

        /// <summary>
        /// Sets the current set of backends.
        /// </summary>
        Task SetBackendsAsync(IDictionary<string, Backend> backends, CancellationToken cancellation);
    }
}

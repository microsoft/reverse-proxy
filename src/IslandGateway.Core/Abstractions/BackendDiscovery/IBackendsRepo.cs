// <copyright file="IBackendsRepo.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IslandGateway.Core.Abstractions
{
    /// <summary>
    /// Manages the set of backends. Changes only become effective when
    /// <see cref="IIslandGatewayConfigManager.ApplyConfigurationsAsync"/> is called.
    /// </summary>
    public interface IBackendsRepo
    {
        /// <summary>
        /// Gets the current set of backends.
        /// </summary>
        Task<IList<Backend>> GetBackendsAsync(CancellationToken cancellation);

        /// <summary>
        /// Sets the current set of backends.
        /// </summary>
        Task SetBackendsAsync(IList<Backend> backends, CancellationToken cancellation);
    }
}

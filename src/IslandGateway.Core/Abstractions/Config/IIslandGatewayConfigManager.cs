// <copyright file="IIslandGatewayConfigManager.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Core.Service;

namespace IslandGateway.Core.Abstractions
{
    /// <summary>
    /// High-level management of Island Gateway state.
    /// </summary>
    public interface IIslandGatewayConfigManager
    {
        /// <summary>
        /// Applies latest configurations obtained from <see cref="IDynamicConfigBuilder"/>.
        /// </summary>
        Task<bool> ApplyConfigurationsAsync(IConfigErrorReporter configErrorReporter, CancellationToken cancellation);
    }
}

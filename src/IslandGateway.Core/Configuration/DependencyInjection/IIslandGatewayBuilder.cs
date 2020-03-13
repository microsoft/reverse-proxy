// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;

namespace IslandGateway.Core.Configuration.DependencyInjection
{
    /// <summary>
    /// Island Gateway builder interface.
    /// </summary>
    public interface IIslandGatewayBuilder
    {
        /// <summary>
        /// Gets the services.
        /// </summary>
        IServiceCollection Services { get; }
    }
}

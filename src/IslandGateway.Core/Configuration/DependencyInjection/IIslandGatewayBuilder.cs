// <copyright file="IIslandGatewayBuilder.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

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
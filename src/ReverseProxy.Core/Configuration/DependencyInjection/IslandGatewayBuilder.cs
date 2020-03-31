// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.Configuration.DependencyInjection
{
    /// <summary>
    /// Island Gateway builder for DI configuration.
    /// </summary>
    internal class IslandGatewayBuilder : IIslandGatewayBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IslandGatewayBuilder"/> class.
        /// </summary>
        /// <param name="services">Services collection.</param>
        public IslandGatewayBuilder(IServiceCollection services)
        {
            Contracts.CheckValue(services, nameof(services));
            Services = services;
        }

        /// <summary>
        /// Gets the services collection.
        /// </summary>
        public IServiceCollection Services { get; }
    }
}

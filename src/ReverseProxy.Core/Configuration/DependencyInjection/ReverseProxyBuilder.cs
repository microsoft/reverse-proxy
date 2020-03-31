// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.Configuration.DependencyInjection
{
    /// <summary>
    /// Reverse Proxy builder for DI configuration.
    /// </summary>
    internal class ReverseProxyBuilder : IReverseProxyBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReverseProxyBuilder"/> class.
        /// </summary>
        /// <param name="services">Services collection.</param>
        public ReverseProxyBuilder(IServiceCollection services)
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

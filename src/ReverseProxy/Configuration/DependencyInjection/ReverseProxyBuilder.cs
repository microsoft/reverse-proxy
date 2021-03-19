// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Yarp.ReverseProxy.Configuration.DependencyInjection
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
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// Gets the services collection.
        /// </summary>
        public IServiceCollection Services { get; }
    }
}

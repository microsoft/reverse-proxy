// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.ReverseProxy.Configuration.DependencyInjection
{
    /// <summary>
    /// Reverse Proxy builder interface.
    /// </summary>
    public interface IReverseProxyBuilder
    {
        /// <summary>
        /// Gets the services.
        /// </summary>
        IServiceCollection Services { get; }
    }
}

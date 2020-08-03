// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Routing;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// High-level management of Reverse Proxy state.
    /// </summary>
    internal interface IProxyConfigManager
    {
        /// <summary>
        /// Load the first data at startup. May throw.
        /// </summary>
        void Load();

        /// <summary>
        /// Exposes endpoints for the route table.
        /// </summary>
        EndpointDataSource DataSource { get; }
    }
}

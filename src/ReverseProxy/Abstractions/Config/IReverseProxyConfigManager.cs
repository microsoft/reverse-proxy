// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Routing;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// High-level management of Reverse Proxy state.
    /// </summary>
    public interface IReverseProxyConfigManager
    {
        void Load();
        EndpointDataSource DataSource { get; }
    }
}

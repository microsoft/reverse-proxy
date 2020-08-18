// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.ReverseProxy.Service
{
    /// <summary>
    /// High-level management of Reverse Proxy state.
    /// </summary>
    internal interface IProxyConfigManager
    {
        /// <summary>
        /// Load the first data at startup. May throw.
        /// </summary>
        Task<EndpointDataSource> InitialLoadAsync();
    }
}

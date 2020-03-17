// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace IslandGateway.Core.Service.Proxy
{
    /// <summary>
    /// Provides a method to proxy the request to the appropriate target.
    /// </summary>
    internal interface IProxyInvoker
    {
        /// <summary>
        /// Proxies the request to the appropriate target.
        /// </summary>
        Task InvokeAsync(HttpContext context);
    }
}

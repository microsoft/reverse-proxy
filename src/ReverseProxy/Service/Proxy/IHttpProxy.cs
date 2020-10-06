// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// Provides a method to proxy an HTTP request to a target server.
    /// </summary>
    public interface IHttpProxy
    {
        /// <summary>
        /// Proxies the incoming request to the destination server, and the response back to the client.
        /// </summary>
        Task ProxyAsync(
            HttpContext context,
            string destinationPrefix,
            HttpMessageInvoker httpClient,
            RequestProxyOptions proxyOptions);
    }
}

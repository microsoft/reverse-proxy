// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// Provides a method to proxy an HTTP request to a target server.
    /// </summary>
    internal interface IHttpProxy
    {
        /// <summary>
        /// Proxies the incoming request to the upstream server, and the response back to our client.
        /// </summary>
        /// <param name="longCancellation">This should be linked to a client disconnect notification like <see cref="HttpContext.RequestAborted"/>
        /// to avoid leaking long running requests.</param>
        Task ProxyAsync(
            HttpContext context,
            string destinationPrefix,
            Transforms transforms,
            IProxyHttpClientFactory httpClientFactory,
            ProxyTelemetryContext proxyTelemetryContext,
            CancellationToken shortCancellation,
            CancellationToken longCancellation);
    }
}

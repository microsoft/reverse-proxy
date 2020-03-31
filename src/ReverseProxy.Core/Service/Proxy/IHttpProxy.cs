// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Core.Service.Proxy.Infra;

namespace Microsoft.ReverseProxy.Core.Service.Proxy
{
    /// <summary>
    /// Provides a method to proxy an HTTP request to a target server.
    /// </summary>
    internal interface IHttpProxy
    {
        /// <summary>
        /// Proxies the incoming request to the upstream server, and the response back to our client.
        /// </summary>
        Task ProxyAsync(
            HttpContext context,
            Uri targetUri,
            IProxyHttpClientFactory httpClientFactory,
            ProxyTelemetryContext proxyTelemetryContext,
            CancellationToken shortCancellation,
            CancellationToken longCancellation);
    }
}

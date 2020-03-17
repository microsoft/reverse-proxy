// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Core.Service.Proxy.Infra;
using Microsoft.AspNetCore.Http;

namespace IslandGateway.Core.Service.Proxy
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

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Middleware
{
    /// <summary>
    /// Blocks incoming requests until the proxy is fully initialized.
    /// </summary>
    public class FullProxyInitializationAwaiterMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IProxyAppState _proxyAppState;

        public FullProxyInitializationAwaiterMiddleware(RequestDelegate next, IProxyAppState proxyAppState)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _proxyAppState = proxyAppState ?? throw new ArgumentNullException(nameof(proxyAppState));
        }

        public async Task Invoke(HttpContext context)
        {
            if (!_proxyAppState.IsFullyInitialized)
            {
                await _proxyAppState.WaitForFullInitialization();
            }

            await _next(context);
        }
    }
}

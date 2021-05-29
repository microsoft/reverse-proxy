// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Service.Proxy.Infrastructure
{
    internal sealed class CallbackProxyHttpClientFactory : ProxyHttpClientFactory
    {
        private readonly Action<ProxyHttpClientContext, SocketsHttpHandler> _configureClient;

        internal CallbackProxyHttpClientFactory(ILogger<ProxyHttpClientFactory> logger,
            Action<ProxyHttpClientContext, SocketsHttpHandler> configureClient) : base(logger)
        {
            _configureClient = configureClient ?? throw new ArgumentNullException(nameof(configureClient));
        }

        protected override void ConfigureHandler(ProxyHttpClientContext context, SocketsHttpHandler handler)
        {
            base.ConfigureHandler(context, handler);
            _configureClient(context, handler);
        }
    }
}

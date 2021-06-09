// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Forwarder
{
    internal sealed class CallbackHttpClientFactory : ForwarderHttpClientFactory
    {
        private readonly Action<ForwarderHttpClientContext, SocketsHttpHandler> _configureClient;

        internal CallbackHttpClientFactory(ILogger<ForwarderHttpClientFactory> logger,
            Action<ForwarderHttpClientContext, SocketsHttpHandler> configureClient) : base(logger)
        {
            _configureClient = configureClient ?? throw new ArgumentNullException(nameof(configureClient));
        }

        protected override void ConfigureHandler(ForwarderHttpClientContext context, SocketsHttpHandler handler)
        {
            base.ConfigureHandler(context, handler);
            _configureClient(context, handler);
        }
    }
}

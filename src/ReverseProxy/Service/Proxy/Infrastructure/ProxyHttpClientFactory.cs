// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;

namespace Microsoft.ReverseProxy.Service.Proxy.Infrastructure
{
    /// <summary>
    /// Default implementation of <see cref="IProxyHttpClientFactory"/>.
    /// </summary>
    internal class ProxyHttpClientFactory : IProxyHttpClientFactory
    {
        /// <summary>
        /// Handler for http requests.
        /// </summary>
        private readonly HttpMessageHandler _handler;

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProxyHttpClientFactory"/> class.
        /// </summary>
        public ProxyHttpClientFactory()
        {
            _handler = new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                MaxConnectionsPerServer = int.MaxValue, // Proxy manages max connections

                // NOTE: MaxResponseHeadersLength = 64, which means up to 64 KB of headers are allowed by default as of .NET Core 3.1.
            };
        }

        /// <inheritdoc/>
        public HttpMessageInvoker CreateClient(ProxyHttpClientContext context)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(typeof(ProxyHttpClientFactory).FullName);
            }

            if (CanReuseOldClient(context))
            {
                return context.OldClient;
            }

            return new HttpMessageInvoker(_handler, disposeHandler: false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Disposes the current instance.
        /// </summary>
        /// <remarks>
        /// This will dispose the underlying <see cref="HttpClientHandler"/>,
        /// so it can only be called after it is no longer in use.
        /// </remarks>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // TODO: This has high potential to cause coding defects. See if we can do better,
                    // perhaps do something like `Microsoft.Extensions.Http.LifetimeTrackingHttpMessageHandler`.
                    _handler.Dispose();
                }

                _disposed = true;
            }
        }

        private bool CanReuseOldClient(ProxyHttpClientContext context)
        {
            if (context.OldClient == null || context.NewOptions != context.OldOptions)
            {
                return false;
            }

            if (!Equals(context.OldMetadata, context.NewMetadata) && context.OldMetadata.Count == context.NewMetadata.Count)
            {
                foreach(var oldPair in context.OldMetadata)
                {
                    if (!context.NewMetadata.TryGetValue(oldPair.Key, out var newValue) || !Equals(oldPair.Value, newValue))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}

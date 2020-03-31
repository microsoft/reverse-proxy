// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace Microsoft.ReverseProxy.Core.Service.Proxy.Infra
{
    /// <summary>
    /// Default implementation of <see cref="IProxyHttpClientFactory"/>.
    /// </summary>
    internal class ProxyHttpClientFactory : IProxyHttpClientFactory
    {
        /// <summary>
        /// Handler for normal (i.e. non-upgradable) http requests.
        /// </summary>
        private readonly HttpMessageHandler _normalHandler;

        /// <summary>
        /// Handler for upgradable http requests.
        /// </summary>
        private HttpMessageHandler _upgradableHandler;

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProxyHttpClientFactory"/> class.
        /// </summary>
        public ProxyHttpClientFactory()
        {
            _normalHandler = new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                MaxConnectionsPerServer = int.MaxValue, // Gateway manages max connections

                // NOTE: MaxResponseHeadersLength = 64, which means up to 64 KB of headers are allowed by default as of .NET Core 3.1.
            };
        }

        private HttpMessageHandler UpgradableHandler
        {
            get
            {
                // NOTE: We don't use Lazy because its lock-free LazyThreadSafetyMode.PublicationOnly mode
                // lacks proper handling of IDisposable values (suprious instances are not properly disposed).
                var handler = Volatile.Read(ref _upgradableHandler);
                if (handler == null)
                {
                    // Optimistically create a new instance hoping we will win a potential race below.
                    // If we lose, we just dispose the spurious instance and take the one that won the race.
                    handler = new SocketsHttpHandler
                    {
                        UseProxy = false,
                        AllowAutoRedirect = false,
                        AutomaticDecompression = DecompressionMethods.None,
                        UseCookies = false,
                        MaxConnectionsPerServer = int.MaxValue, // Gateway manages max connections
                        PooledConnectionLifetime = TimeSpan.Zero, // Do not reuse connections
                    };

                    HttpMessageHandler existingHandler;
                    if ((existingHandler = Interlocked.CompareExchange(ref _upgradableHandler, handler, null)) != null)
                    {
                        handler.Dispose();
                        handler = existingHandler;
                    }
                }

                return handler;
            }
        }

        /// <inheritdoc/>
        public HttpMessageInvoker CreateNormalClient()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(typeof(ProxyHttpClientFactory).FullName);
            }

            return new HttpMessageInvoker(_normalHandler, disposeHandler: false);
        }

        /// <inheritdoc/>
        public HttpMessageInvoker CreateUpgradableClient()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(typeof(ProxyHttpClientFactory).FullName);
            }

            return new HttpMessageInvoker(UpgradableHandler, disposeHandler: false);
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
                    _normalHandler.Dispose();

                    var currentUpgradableHandler = Volatile.Read(ref _upgradableHandler);
                    if (currentUpgradableHandler != null)
                    {
                        if (Interlocked.CompareExchange(ref _upgradableHandler, null, currentUpgradableHandler) == currentUpgradableHandler)
                        {
                        }
                    }
                }

                _disposed = true;
            }
        }
    }
}

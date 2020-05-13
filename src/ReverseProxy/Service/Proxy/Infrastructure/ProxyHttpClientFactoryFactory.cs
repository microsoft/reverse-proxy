// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.Proxy.Infrastructure
{
    /// <summary>
    /// Default implementation of <see cref="IProxyHttpClientFactoryFactory"/>.
    /// </summary>
    internal class ProxyHttpClientFactoryFactory : IProxyHttpClientFactoryFactory
    {
        /// <inheritdoc/>
        public IProxyHttpClientFactory CreateFactory()
        {
            return new ProxyHttpClientFactory();
        }
    }
}

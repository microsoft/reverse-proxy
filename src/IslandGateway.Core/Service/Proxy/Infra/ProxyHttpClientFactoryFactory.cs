// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Core.Service.Proxy.Infra
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

// <copyright file="ProxyHttpClientFactoryFactory.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace IslandGateway.Core.Service.Proxy.Infra
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

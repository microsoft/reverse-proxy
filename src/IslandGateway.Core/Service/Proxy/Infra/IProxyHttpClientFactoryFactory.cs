// <copyright file="IProxyHttpClientFactoryFactory.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Net.Http;

namespace IslandGateway.Core.Service.Proxy.Infra
{
    /// <summary>
    /// Provides a method to create instances of <see cref="IProxyHttpClientFactory"/>
    /// which in turn can create instances of <see cref="HttpClient"/>
    /// for proxying requests to an upstream server.
    /// </summary>
    internal interface IProxyHttpClientFactoryFactory
    {
        /// <summary>
        /// Creates and configures an <see cref="HttpClient"/> instance
        /// that can be used for proxying requests to an upstream server.
        /// </summary>
        IProxyHttpClientFactory CreateFactory();
    }
}

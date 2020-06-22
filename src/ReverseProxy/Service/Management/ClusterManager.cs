// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.Management
{
    internal sealed class ClusterManager : ItemManagerBase<ClusterInfo>, IClusterManager
    {
        private readonly IDestinationManagerFactory _destinationManagerFactory;
        private readonly IProxyHttpClientFactoryFactory _httpClientFactoryFactory;

        public ClusterManager(IDestinationManagerFactory destinationManagerFactory, IProxyHttpClientFactoryFactory httpClientFactoryFactory)
        {
            _destinationManagerFactory = destinationManagerFactory ?? throw new ArgumentNullException(nameof(destinationManagerFactory));
            _httpClientFactoryFactory = httpClientFactoryFactory ?? throw new ArgumentNullException(nameof(httpClientFactoryFactory));
        }

        /// <inheritdoc/>
        protected override ClusterInfo InstantiateItem(string itemId)
        {
            var destinationManager = _destinationManagerFactory.CreateDestinationManager();
            var httpClientFactory = _httpClientFactoryFactory.CreateFactory();
            return new ClusterInfo(itemId, destinationManager, httpClientFactory);
        }
    }
}

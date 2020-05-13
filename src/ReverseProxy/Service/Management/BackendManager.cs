// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.Management
{
    internal sealed class BackendManager : ItemManagerBase<BackendInfo>, IBackendManager
    {
        private readonly IDestinationManagerFactory _destinationManagerFactory;
        private readonly IProxyHttpClientFactoryFactory _httpClientFactoryFactory;

        public BackendManager(IDestinationManagerFactory destinationManagerFactory, IProxyHttpClientFactoryFactory httpClientFactoryFactory)
        {
            Contracts.CheckValue(destinationManagerFactory, nameof(destinationManagerFactory));
            Contracts.CheckValue(httpClientFactoryFactory, nameof(httpClientFactoryFactory));

            _destinationManagerFactory = destinationManagerFactory;
            _httpClientFactoryFactory = httpClientFactoryFactory;
        }

        /// <inheritdoc/>
        protected override BackendInfo InstantiateItem(string itemId)
        {
            var destinationManager = _destinationManagerFactory.CreateDestinationManager();
            var httpClientFactory = _httpClientFactoryFactory.CreateFactory();
            return new BackendInfo(itemId, destinationManager, httpClientFactory);
        }
    }
}

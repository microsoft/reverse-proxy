// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Core.Service.Proxy.Infra;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.Service.Management
{
    internal sealed class BackendManager : ItemManagerBase<BackendInfo>, IBackendManager
    {
        private readonly IEndpointManagerFactory _endpointManagerFactory;
        private readonly IProxyHttpClientFactoryFactory _httpClientFactoryFactory;

        public BackendManager(IEndpointManagerFactory endpointManagerFactory, IProxyHttpClientFactoryFactory httpClientFactoryFactory)
        {
            Contracts.CheckValue(endpointManagerFactory, nameof(endpointManagerFactory));
            Contracts.CheckValue(httpClientFactoryFactory, nameof(httpClientFactoryFactory));

            _endpointManagerFactory = endpointManagerFactory;
            _httpClientFactoryFactory = httpClientFactoryFactory;
        }

        /// <inheritdoc/>
        protected override BackendInfo InstantiateItem(string itemId)
        {
            var endpointManager = _endpointManagerFactory.CreateEndpointManager();
            var httpClientFactory = _httpClientFactoryFactory.CreateFactory();
            return new BackendInfo(itemId, endpointManager, httpClientFactory);
        }
    }
}

// <copyright file="BackendManager.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.Core.RuntimeModel;
using IslandGateway.Core.Service.Proxy.Infra;
using IslandGateway.Utilities;

namespace IslandGateway.Core.Service.Management
{
    internal sealed class BackendManager : ItemManagerBase<BackendInfo>, IBackendManager
    {
        private readonly IEndpointManagerFactory endpointManagerFactory;
        private readonly IProxyHttpClientFactoryFactory httpClientFactoryFactory;

        public BackendManager(IEndpointManagerFactory endpointManagerFactory, IProxyHttpClientFactoryFactory httpClientFactoryFactory)
        {
            Contracts.CheckValue(endpointManagerFactory, nameof(endpointManagerFactory));
            Contracts.CheckValue(httpClientFactoryFactory, nameof(httpClientFactoryFactory));

            this.endpointManagerFactory = endpointManagerFactory;
            this.httpClientFactoryFactory = httpClientFactoryFactory;
        }

        /// <inheritdoc/>
        protected override BackendInfo InstantiateItem(string itemId)
        {
            var endpointManager = this.endpointManagerFactory.CreateEndpointManager();
            var httpClientFactory = this.httpClientFactoryFactory.CreateFactory();
            return new BackendInfo(itemId, endpointManager, httpClientFactory);
        }
    }
}

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
        private readonly IEndpointManagerFactory _endpointManagerFactory;
        private readonly IProxyHttpClientFactoryFactory _httpClientFactoryFactory;

        public BackendManager(IEndpointManagerFactory endpointManagerFactory, IProxyHttpClientFactoryFactory httpClientFactoryFactory)
        {
            Contracts.CheckValue(endpointManagerFactory, nameof(endpointManagerFactory));
            Contracts.CheckValue(httpClientFactoryFactory, nameof(httpClientFactoryFactory));

            this._endpointManagerFactory = endpointManagerFactory;
            this._httpClientFactoryFactory = httpClientFactoryFactory;
        }

        /// <inheritdoc/>
        protected override BackendInfo InstantiateItem(string itemId)
        {
            var endpointManager = this._endpointManagerFactory.CreateEndpointManager();
            var httpClientFactory = this._httpClientFactoryFactory.CreateFactory();
            return new BackendInfo(itemId, endpointManager, httpClientFactory);
        }
    }
}

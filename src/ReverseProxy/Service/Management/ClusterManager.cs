// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Management
{
    internal sealed class ClusterManager : ItemManagerBase<ClusterInfo>, IClusterManager
    {
        private readonly IDestinationManagerFactory _destinationManagerFactory;

        public ClusterManager(IDestinationManagerFactory destinationManagerFactory)
        {
            _destinationManagerFactory = destinationManagerFactory ?? throw new ArgumentNullException(nameof(destinationManagerFactory));
        }

        /// <inheritdoc/>
        protected override ClusterInfo InstantiateItem(string itemId)
        {
            var destinationManager = _destinationManagerFactory.CreateDestinationManager();
            return new ClusterInfo(itemId, destinationManager);
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using IslandGateway.Core.RuntimeModel;

namespace IslandGateway.Core.Service.Management
{
    internal sealed class EndpointManager : ItemManagerBase<EndpointInfo>, IEndpointManager
    {
        /// <inheritdoc/>
        protected override EndpointInfo InstantiateItem(string itemId)
        {
            return new EndpointInfo(itemId);
        }
    }
}

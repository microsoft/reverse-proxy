// <copyright file="EndpointManager.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

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

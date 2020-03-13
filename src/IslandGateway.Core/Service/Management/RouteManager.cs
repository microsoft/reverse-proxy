// <copyright file="RouteManager.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.Core.RuntimeModel;

namespace IslandGateway.Core.Service.Management
{
    internal sealed class RouteManager : ItemManagerBase<RouteInfo>, IRouteManager
    {
        /// <inheritdoc/>
        protected override RouteInfo InstantiateItem(string itemId)
        {
            return new RouteInfo(itemId);
        }
    }
}

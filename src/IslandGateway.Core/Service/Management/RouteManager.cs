// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Management
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

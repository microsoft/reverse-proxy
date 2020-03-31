// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace Microsoft.ReverseProxy.Core.Service.Management
{
    /// <summary>
    /// Manages routes and their runtime states.
    /// </summary>
    internal interface IRouteManager : IItemManager<RouteInfo>
    {
    }
}

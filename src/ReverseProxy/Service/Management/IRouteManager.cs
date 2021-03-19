// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.Management
{
    /// <summary>
    /// Manages routes and their runtime states.
    /// </summary>
    internal interface IRouteManager : IItemManager<RouteInfo>
    {
    }
}

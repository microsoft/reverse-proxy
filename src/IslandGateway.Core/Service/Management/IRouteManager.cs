// <copyright file="IRouteManager.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.Core.RuntimeModel;

namespace IslandGateway.Core.Service.Management
{
    /// <summary>
    /// Manages routes and their runtime states.
    /// </summary>
    internal interface IRouteManager : IItemManager<RouteInfo>
    {
    }
}
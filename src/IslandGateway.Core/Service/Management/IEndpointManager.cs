// <copyright file="IEndpointManager.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.Core.RuntimeModel;

namespace IslandGateway.Core.Service.Management
{
    /// <summary>
    /// Manages the runtime state of endpoints in a backend.
    /// </summary>
    internal interface IEndpointManager : IItemManager<EndpointInfo>
    {
    }
}
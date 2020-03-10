// <copyright file="IBackendManager.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.Core.RuntimeModel;

namespace IslandGateway.Core.Service.Management
{
    /// <summary>
    /// Manages the runtime state of backends.
    /// </summary>
    internal interface IBackendManager : IItemManager<BackendInfo>
    {
    }
}
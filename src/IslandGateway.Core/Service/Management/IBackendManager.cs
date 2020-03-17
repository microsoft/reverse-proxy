// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

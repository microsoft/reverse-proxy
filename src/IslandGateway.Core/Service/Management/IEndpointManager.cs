// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

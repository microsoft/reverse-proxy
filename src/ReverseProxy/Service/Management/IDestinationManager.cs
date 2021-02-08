// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Management
{
    /// <summary>
    /// Manages the runtime state of destinations in a cluster.
    /// </summary>
    public interface IDestinationManager : IItemManager<DestinationInfo>
    {
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace Microsoft.ReverseProxy.Core.Service.Management
{
    /// <summary>
    /// Manages the runtime state of destinations in a backend.
    /// </summary>
    internal interface IDestinationManager : IItemManager<DestinationInfo>
    {
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.Management
{
    /// <summary>
    /// Manages the runtime state of destinations in a cluster.
    /// </summary>
    internal interface IDestinationManager : IItemManager<DestinationInfo>
    {
    }
}

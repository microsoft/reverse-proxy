// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Management
{
    /// <summary>
    /// Manages the runtime state of backends.
    /// </summary>
    internal interface IBackendManager : IItemManager<BackendInfo>
    {
    }
}

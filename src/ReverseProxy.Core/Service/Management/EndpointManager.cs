// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace Microsoft.ReverseProxy.Core.Service.Management
{
    internal sealed class EndpointManager : ItemManagerBase<EndpointInfo>, IEndpointManager
    {
        /// <inheritdoc/>
        protected override EndpointInfo InstantiateItem(string itemId)
        {
            return new EndpointInfo(itemId);
        }
    }
}

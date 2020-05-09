// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace Microsoft.ReverseProxy.Core.Service.Management
{
    internal sealed class DestinationManager : ItemManagerBase<DestinationInfo>, IDestinationManager
    {
        /// <inheritdoc/>
        protected override DestinationInfo InstantiateItem(string itemId)
        {
            return new DestinationInfo(itemId);
        }
    }
}

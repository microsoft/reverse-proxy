// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.Management
{
    internal sealed class DestinationManager : ItemManagerBase<DestinationInfo>, IDestinationManager
    {
        protected override DestinationInfo InstantiateItem(string itemId)
        {
            return new DestinationInfo(itemId);
        }
    }
}

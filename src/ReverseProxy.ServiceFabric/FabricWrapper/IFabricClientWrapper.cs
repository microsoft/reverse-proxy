// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Fabric;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    internal interface IFabricClientWrapper
    {
        FabricClient FabricClient { get; }
    }
}
